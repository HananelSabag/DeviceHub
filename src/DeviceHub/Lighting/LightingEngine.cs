using System.Diagnostics;
using DeviceHub.Audio;
using DeviceHub.Devices;
using DeviceHub.Interop;
using Microsoft.Win32.SafeHandles;

namespace DeviceHub.Lighting;

/// <summary>
/// Drives the keyboard's lights and its OLED screen on one background thread, whether the window is open or
/// DeviceHub sits in the tray. With DeviceHub's lighting off and the screen off it does nothing at all; a still
/// colour is sent once; animated effects run at 20 frames a second; the screen redraws only when its picture changes.
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
        lock (gate)
        {
            if (newLighting != lighting) { lighting = newLighting; lightsDirty = true; }
            if (newScreen != screen || newPicture is not null) { screen = newScreen; screenDirty = true; screenDue = 0; pictureFrame = 0; }
            if (newPicture is not null) picture = newPicture;
        }
        wake.Set();
    }

    /// <summary>The latest readings, for the screen's device page.</summary>
    public void SetDevices(IReadOnlyList<DeviceReading> list)
    {
        devices = list;
        if (screen.Mode == "devices") { screenDue = 0; wake.Set(); }
    }

    /// <summary>Something was plugged in or out: look for the keyboard again and repaint it.</summary>
    public void DevicesChanged()
    {
        lock (gate) { lastLook = -10_000; lightsDirty = true; screenDirty = true; }
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
            catch (IOException) { Disconnect(); wait = 3000; } // unplugged mid-frame: look again shortly
            catch (Exception ex) { Debug.WriteLine($"lighting: {ex}"); wait = 3000; }
            wake.WaitOne(wait);
        }
        lock (gate) Disconnect();
    }

    /// <summary>One pass: lights, then the screen. Returns how long to sleep.</summary>
    private int Step()
    {
        var now = clock.ElapsedMilliseconds;
        var wait = 5000;
        // The sound level, only when something follows the music (it is created on this thread, and stays on it).
        if ((lighting.Enabled && lighting.Effect == "music") || screen.Mode is "sound" or "stars")
            level = (sound ??= new SoundLevel()).Read();

        if (lighting.Enabled)
        {
            if (KeyboardEffects.Animated(lighting))
            {
                ApexKeyboard.SendColors(handle!, KeyboardEffects.Frame(lighting, now / 1000.0, level));
                wait = 50;
            }
            else if (lightsDirty) ApexKeyboard.SendColors(handle!, KeyboardEffects.Frame(lighting, 0));
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

    private void Show(byte[] bits)
    {
        ApexKeyboard.SendScreen(handle!, bits);
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
}
