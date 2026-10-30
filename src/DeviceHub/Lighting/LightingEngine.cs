using System.Diagnostics;
using System.Runtime.InteropServices;
using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;
using DeviceHub.Interop;
using Microsoft.Win32.SafeHandles;

namespace DeviceHub.Lighting;

/// <summary>What the controller's lightbar does. static: set once by DeviceControls; the others are driven here.</summary>
internal sealed record PadEffect(string Mode, DualSense.Lights Lights, int Speed = 5);

/// <summary>
/// Drives the keyboard's lights and its OLED screen, and the controller's moving lightbar, on one background thread,
/// whether the window is open or DeviceHub sits in the tray. With nothing to animate it does nothing at all;
/// a still colour is sent once; moving effects run at 20 frames a second; the screen redraws only when its picture
/// changes. The keyboard keeps nothing of this itself: its "direct mode" shows what it is sent, live, the way
/// SteelSeries GG drives it, so these effects need DeviceHub running (in the tray is enough).
/// </summary>
internal sealed class LightingEngine : IDisposable
{
    public static readonly LightingEngine Instance = new();

    private readonly Lock gate = new();
    private readonly AutoResetEvent wake = new(false);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private Thread? thread;
    private volatile bool stopping;

    private KeyboardLighting lighting = new();
    private OledSettings screen = new();
    private List<(byte[] Bits, int DelayMs)> picture = [];
    private IReadOnlyList<DeviceReading> devices = [];

    private SafeFileHandle? handle;
    private HidInfo? keyboard;
    private long lastLook = -10_000;
    private bool lightsDirty, screenDirty, lightsWereOn;
    private byte[]? lastScreen;
    private long screenDue;
    private int pictureFrame;
    private NowPlaying.Track? track;
    private long trackAsked = -10_000;
    private SoundLevel? sound;
    private float level;
    private ScreenAnimation? animation;
    private string? animationMode;
    private long animationStart;
    private DeviceReading? laptop;
    private long laptopAsked = -10_000;
    private int? heatShown;
    private readonly List<KeyboardEffects.Press> presses = [];
    private long lastPress = -100_000;
    private bool restingSent;

    private PadEffect pad = new("static", new(0, 60, 255, 1, false));
    private FileStream? padStream;
    private HidInfo? padDevice;
    private long padLook = -10_000, padGameCheck = -10_000, padBatterySent = -100_000;
    private bool gameInFront;

    /// <summary>Frames sent so far, for the health check in Settings.</summary>
    public long KeyFrames { get; private set; }
    public long ScreenFrames { get; private set; }
    public long PadFrames { get; private set; }
    public bool KeyboardConnected => handle is { IsInvalid: false, IsClosed: false };

    /// <summary>Raised when an effect starts or stops needing key presses; the window listens only while one does.</summary>
    public event Action<bool>? KeysWantedChanged;
    public bool KeysWanted => KeyboardEffects.WantsKeys(lighting);

    /// <summary>A key on the Apex went down (HID usage code).</summary>
    public void KeyPressed(byte code)
    {
        lock (gate)
        {
            var now = clock.ElapsedMilliseconds;
            presses.RemoveAll(p => now / 1000.0 - p.T > 4);
            if (presses.Count < 64) presses.Add(new(code, now / 1000.0));
            lastPress = now;
        }
        wake.Set();
    }

    /// <summary>Raised (on the engine's thread) with every new picture sent to the screen.</summary>
    public event Action<byte[]>? ScreenChanged;
    public byte[]? CurrentScreen => lastScreen;
    public bool English { get; set; }

    public void Start()
    {
        if (thread is not null) return;
        thread = new Thread(Loop) { IsBackground = true, Name = "DeviceHub lighting", Priority = ThreadPriority.BelowNormal };
        thread.Start();
    }

    public void Configure(KeyboardLighting newLighting, OledSettings newScreen, List<(byte[] Bits, int DelayMs)>? newPicture = null)
    {
        var wanted = KeysWanted;
        lock (gate)
        {
            if (newLighting != lighting) { lighting = newLighting; lightsDirty = true; restingSent = false; heatShown = null; }
            if (newScreen != screen || newPicture is not null) { screen = newScreen; screenDirty = true; screenDue = 0; pictureFrame = 0; }
            if (newPicture is not null) picture = newPicture;
        }
        if (KeysWanted != wanted) KeysWantedChanged?.Invoke(KeysWanted);
        wake.Set();
    }

    public void ConfigurePad(PadEffect effect)
    {
        lock (gate) { pad = effect; padBatterySent = -100_000; }
        wake.Set();
    }

    /// <summary>The latest readings, for the screen's device page and the lightbar's battery colour.</summary>
    public void SetDevices(IReadOnlyList<DeviceReading> list)
    {
        devices = list;
        if (screen.Mode == "devices" || pad.Mode == "battery") { screenDue = 0; padBatterySent = -100_000; wake.Set(); }
    }

    /// <summary>Something was plugged in or out: look for the keyboard and the controller again and repaint them.</summary>
    public void DevicesChanged()
    {
        lock (gate) { lastLook = -10_000; padLook = -10_000; lightsDirty = true; screenDirty = true; padBatterySent = -100_000; }
        wake.Set();
    }

    private bool Busy => lighting.Enabled || screen.Mode != "off" || lightsDirty || screenDirty;

    private void Loop()
    {
        while (!stopping)
        {
            var wait = 5000;
            try
            {
                lock (gate)
                {
                    if (Busy && Connected()) wait = Step();
                }
            }
            catch (IOException) { lock (gate) Disconnect(); wait = 3000; } // unplugged mid-frame: look again shortly
            catch (Exception ex) { Log.Error("lighting", ex); wait = 3000; }
            try
            {
                lock (gate) wait = Math.Min(wait, PadStep());
            }
            catch (IOException) { lock (gate) DropPad(); }
            catch (Exception ex) { Log.Error("lightbar", ex); }
            wake.WaitOne(wait);
        }
        lock (gate) { Disconnect(); DropPad(); }
    }

    /// <summary>One pass: lights, then the screen. Returns how long to sleep.</summary>
    private int Step()
    {
        var now = clock.ElapsedMilliseconds;
        var wait = 5000;
        // The sound level, only when something follows the music (it is created on this thread, and stays on it).
        if ((lighting.Enabled && lighting.Effect is "music" or "visualizer") || screen.Mode is "sound" or "stars" or "show")
            level = (sound ??= new SoundLevel()).Read();
        // The laptop's sensors, for the temperature effect and the CPU / GPU screen, every 2 seconds.
        if ((KeyboardEffects.FollowsHeat(lighting) || screen.Mode == "system") && now - laptopAsked >= 2000)
        {
            laptop = DeviceReader.ReadLaptop();
            laptopAsked = now;
        }

        if (lighting.Enabled)
        {
            if (KeyboardEffects.WantsKeys(lighting))
            {
                // Key presses: 30 frames a second while a press is still showing, then one resting frame and quiet.
                var showing = now - lastPress < KeyboardEffects.Fade(lighting) * 1700;
                if (showing || !restingSent || lightsDirty)
                {
                    Send(KeyboardEffects.Frame(lighting, now / 1000.0, level, presses));
                    restingSent = !showing;
                }
                wait = showing ? 33 : 1000;
            }
            else if (KeyboardEffects.FollowsHeat(lighting))
            {
                // A new colour only when the temperature moves.
                var heat = laptop?.Temperature;
                if (lightsDirty || heat != heatShown) { Send(KeyboardEffects.Frame(lighting, 0, temperature: heat)); heatShown = heat; }
                wait = 2000;
            }
            else if (KeyboardEffects.Animated(lighting))
            {
                Send(KeyboardEffects.Frame(lighting, now / 1000.0, level));
                wait = 50;
            }
            else if (lightsDirty) Send(KeyboardEffects.Frame(lighting, 0));
            lightsWereOn = true;
        }
        else if (lightsDirty && lightsWereOn)
        {
            ApexKeyboard.UseOwnLighting(keyboard!); // switched off: the keyboard's own lighting comes back
            lightsWereOn = false;
        }
        lightsDirty = false;

        if (screen.Mode != "off")
        {
            if (now >= screenDue || screenDirty)
            {
                var (bits, next) = Render(now);
                if (screenDirty || lastScreen is null || !bits.AsSpan().SequenceEqual(lastScreen)) Show(bits);
                screenDue = now + next;
            }
            wait = (int)Math.Clamp(screenDue - now, 10, wait);
        }
        else if (screenDirty && lastScreen is not null)
        {
            Show(new byte[640]); // off: a dark screen rather than a still picture left to burn in
            lastScreen = null;
        }
        screenDirty = false;
        return wait;
    }

    private void Send(Dictionary<byte, Rgb> colors)
    {
        ApexKeyboard.SendColors(handle!, colors);
        KeyFrames++;
    }

    private void Show(byte[] bits)
    {
        ApexKeyboard.SendScreen(handle!, bits);
        ScreenFrames++;
        lastScreen = bits;
        ScreenChanged?.Invoke(bits);
    }

    /// <summary>The screen's next picture and how long until the one after.</summary>
    private (byte[] Bits, int NextMs) Render(long now)
    {
        var time = DateTime.Now;
        if (ScreenAnimation.For(screen.Mode) is not null)
        {
            if (animationMode != screen.Mode) { animation = ScreenAnimation.For(screen.Mode); animationMode = screen.Mode; animationStart = now; }
            return (animation!.Frame((now - animationStart) / 1000.0, level), 50);
        }
        animation = null;
        animationMode = null;
        var mode = screen.Mode;
        if (mode == "cycle")
        {
            // Ten seconds each: the clock, the batteries, and the song when one is playing.
            if (now - trackAsked >= 1000) { track = NowPlaying.GetAsync().GetAwaiter().GetResult(); trackAsked = now; }
            var slots = track is null ? new[] { "clock", "devices" } : ["clock", "devices", "music"];
            mode = slots[(int)(now / 10_000 % slots.Length)];
            var (bits, next) = Render(now, mode, time);
            return (bits, Math.Min(next, (int)(10_000 - now % 10_000) + 5));
        }
        return Render(now, mode, time);
    }

    private (byte[] Bits, int NextMs) Render(long now, string mode, DateTime time)
    {
        switch (mode)
        {
            case "devices":
                return (OledScreen.Devices(devices, OledScreen.Shift(time)), 5000);
            case "system":
                return (OledScreen.Workload(laptop, OledScreen.Shift(time)), 2000);
            case "analog":
                return (OledScreen.Analog(time, English), 1000 - time.Millisecond + 5);
            case "text":
                return (OledScreen.Text(screen.Text, OledScreen.Shift(time)), 30_000);
            case "image" when picture.Count > 0:
                var (bits, delay) = picture[pictureFrame % picture.Count];
                pictureFrame++;
                return (bits, picture.Count == 1 ? 60_000 : delay);
            case "image":
                return (OledScreen.Text("DeviceHub", OledScreen.Shift(time)), 30_000);
            case "music":
                if (now - trackAsked >= 1000)
                {
                    track = NowPlaying.GetAsync().GetAwaiter().GetResult();
                    trackAsked = now;
                }
                if (track is not null) return (OledScreen.Music(track, now / 1000.0), 80);
                goto default; // nothing playing: the clock
            default:
                return (OledScreen.Clock(time, English), 1000 - time.Millisecond + 5);
        }
    }

    // ---- The controller's lightbar ----

    /// <summary>
    /// Breathing and rainbow at 15 frames a second, the battery colour once a minute; nothing while a full-screen
    /// game is in front (so DeviceHub never talks to the controller mid-game). Returns how long to sleep.
    /// </summary>
    private int PadStep()
    {
        if (pad.Mode is not ("breathing" or "rainbow" or "battery")) { DropPad(); return 5000; }
        var now = clock.ElapsedMilliseconds;
        if (now - padGameCheck >= 2000)
        {
            padGameCheck = now;
            gameInFront = SHQueryUserNotificationState(out var state) == 0 && state is 2 or 3; // busy (full screen) or a full-screen D3D game
        }
        if (gameInFront) return 2000;
        if (padStream is null)
        {
            if (now - padLook < 5000) return 5000;
            padLook = now;
            padDevice = Hid.Enumerate(DualSense.Vendor).FirstOrDefault(x => x.Pid is DualSense.DualSensePid or DualSense.EdgePid && x.UsagePage == 0x01 && x.Usage == 0x05 && !x.Bluetooth);
            padStream = padDevice is null ? null : Hid.OpenStream(padDevice.Path, write: true);
            if (padStream is null) return 5000;
        }

        var l = pad.Lights;
        Rgb color;
        var wait = 66;
        switch (pad.Mode)
        {
            case "breathing":
                color = new Rgb((byte)l.Red, (byte)l.Green, (byte)l.Blue).Scale(0.1 + 0.9 * Math.Pow(Math.Sin(Math.PI * now / 1000.0 / (8.5 - pad.Speed * 0.7)), 2));
                break;
            case "rainbow":
                color = Rgb.Hue(now / 1000.0 * pad.Speed * 0.02);
                break;
            default:
                // Battery: green, then yellow below half, red below a fifth; sent again every minute or when it changes.
                if (now - padBatterySent < 60_000) return 5000;
                var battery = devices.FirstOrDefault(d => d.Id == "controller")?.Battery;
                color = battery is null ? new Rgb(30, 91, 255) : battery >= 50 ? new Rgb(43, 234, 90) : battery >= 20 ? new Rgb(255, 208, 0) : new Rgb(255, 42, 42);
                padBatterySent = now;
                wait = 5000;
                break;
        }
        padStream.Write(DualSense.LightsReport(new DualSense.Lights(color.R, color.G, color.B, l.Player, l.MicLed), padDevice!.OutputLength));
        PadFrames++;
        return wait;
    }

    private void DropPad()
    {
        padStream?.Dispose();
        padStream = null;
    }

    /// <summary>An open handle to the keyboard, looking for it at most every 5 seconds.</summary>
    private bool Connected()
    {
        if (handle is { IsInvalid: false, IsClosed: false }) return true;
        var now = clock.ElapsedMilliseconds;
        if (now - lastLook < 5000) return false;
        lastLook = now;
        keyboard = ApexKeyboard.Find(Hid.Enumerate(ApexKeyboard.Vendor));
        handle = keyboard is null ? null : ApexKeyboard.Open(keyboard);
        if (handle is null) return false;
        lightsDirty = lighting.Enabled; // a keyboard just plugged in has forgotten everything
        screenDirty = screen.Mode != "off";
        return true;
    }

    private void Disconnect()
    {
        handle?.Dispose();
        handle = null;
        lastLook = clock.ElapsedMilliseconds;
    }

    public void Dispose()
    {
        stopping = true;
        wake.Set();
        thread?.Join(1000);
    }

    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
}
