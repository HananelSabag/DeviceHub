using System.Diagnostics;
using System.Text.Json;
using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;
using DeviceHub.Interop;
using DeviceHub.Lighting;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeviceHub;

/// <summary>
/// The host window: a WebView2 showing wwwroot. The page talks to us with chrome.webview.postMessage({ cmd })
/// and we answer with { type, data }.
/// Open: the laptop sensors update every 3 seconds and the wireless devices every minute (and right away when
/// something is plugged in or out). In the tray: the page is suspended and the devices are read every 2 minutes,
/// for the tray's tooltip, low-battery notes and the keyboard screen. Sound devices are never polled.
/// </summary>
internal sealed class MainForm : Form
{
    public const string ShowSignal = "DeviceHub.Show";
    private const string Host = "devicehub.example";
    private const int WmDeviceChange = 0x0219;
    private static readonly Color Backdrop = Color.FromArgb(26, 29, 35);

    private readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Backdrop };
    private readonly System.Windows.Forms.Timer sensorTimer = new() { Interval = 3000 };
    private readonly System.Windows.Forms.Timer deviceTimer = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer plugDebounce = new() { Interval = 1500 };
    private readonly System.Windows.Forms.Timer audioDebounce = new() { Interval = 250 };
    private readonly NotifyIcon tray = new();
    private readonly EventWaitHandle showSignal = new(false, EventResetMode.AutoReset, ShowSignal);
    private readonly HashSet<string> warned = []; // devices already warned about since they dropped low
    private AppSettings settings = AppSettings.Load();
    private AudioWatcher? audioWatcher;
    private bool startHidden;
    private bool exiting;
    private bool pageReady;
    private bool watchScreen;
    private bool readingDevices;
    private bool readingSensors;
    private List<DeviceReading> lastDevices = [];
    private DeviceReading? lastLaptop;

    public MainForm(bool startHidden)
    {
        this.startHidden = startHidden;
        I18n.English = settings.Language == "en";
        Text = "DeviceHub";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor = Backdrop;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = LogicalToDeviceUnits(new Size(980, 640));
        Size = LogicalToDeviceUnits(new Size(1280, 800));
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(web);

        tray.Icon = Icon;
        tray.Text = "DeviceHub";
        tray.Visible = true;
        // One click on the icon opens the window; the right button opens the quick menu.
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };
        BuildTrayMenu();

        Load += async (_, _) =>
        {
            try { await InitWebAsync(); }
            catch (Exception ex) { ReportError("startup", ex); }
        };
        Resize += async (_, _) =>
        {
            try { await OnResizedAsync(); }
            catch (Exception ex) { ReportError("resize", ex); }
        };
        FormClosing += (_, e) =>
        {
            // The X, Alt+F4 or the taskbar's "Close window" (a plain WM_CLOSE comes as None); not Windows shutting down.
            if (!exiting && settings.KeepInTray && e.CloseReason is CloseReason.UserClosing or CloseReason.None)
            {
                e.Cancel = true; // to the tray: the keyboard's lights and screen keep going
                HideToTray();
                return;
            }
            Log.Info($"closing: {e.CloseReason}{(exiting ? " (Exit from the tray menu)" : "")}");
            sensorTimer.Stop();
            deviceTimer.Stop();
            audioWatcher?.Dispose();
            tray.Visible = false;
        };
        sensorTimer.Tick += (_, _) => ReadSensors();
        deviceTimer.Tick += (_, _) => ReadDevices();
        plugDebounce.Tick += (_, _) => { plugDebounce.Stop(); ReadDevices(); };
        audioDebounce.Tick += (_, _) => { audioDebounce.Stop(); PostAudio(); };
        LightingEngine.Instance.ScreenChanged += bits => { if (watchScreen) OnUiThread(() => Post("screen", Convert.ToBase64String(bits))); };
        LightingEngine.Instance.KeysWantedChanged += on => OnUiThread(() => RawKeys.Listen(Handle, on));

        // A second DeviceHub started while this one is in the tray asks us to come forward.
        new Thread(() => { while (showSignal.WaitOne()) OnUiThread(ShowFromTray); }) { IsBackground = true, Name = "DeviceHub show" }.Start();
    }

    /// <summary>Started with Windows: create the window but keep it hidden, and read the devices in the background.</summary>
    protected override void SetVisibleCore(bool value)
    {
        if (startHidden)
        {
            startHidden = false;
            if (!IsHandleCreated) CreateHandle();
            value = false;
            deviceTimer.Interval = 120_000;
            deviceTimer.Start();
            ReadDevices();
        }
        base.SetVisibleCore(value);
    }

    /// <summary>
    /// The quick menu on the tray icon, filled each time it opens: every battery and the laptop's heat at the top,
    /// then the sound device, the keyboard's lighting and screen and the lightbar, one click each; Open and Exit last.
    /// </summary>
    private void BuildTrayMenu()
    {
        var menu = new ContextMenuStrip { RightToLeft = I18n.English ? RightToLeft.No : RightToLeft.Yes, ShowCheckMargin = true, ShowImageMargin = false };
        menu.Opening += (_, _) => FillTrayMenu(menu);
        tray.ContextMenuStrip?.Dispose();
        tray.ContextMenuStrip = menu;
    }

    private void FillTrayMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        var info = new ToolStripMenuItem("DeviceHub") { Enabled = false, Font = new Font(menu.Font, FontStyle.Bold) };
        menu.Items.Add(info);
        foreach (var d in lastDevices.Where(d => d.Battery is not null && d.State == DeviceState.Ready))
            menu.Items.Add(new ToolStripMenuItem($"{Short(d.Id)}  {d.Battery}%{(d.Charging ? " ⚡" : "")}") { Enabled = false });
        if (lastLaptop is { Temperature: { } cpu } l)
            menu.Items.Add(new ToolStripMenuItem($"CPU {cpu}°{(l.GpuTemperature is { } gpu ? $"  ·  GPU {gpu}°" : "")}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        // Sound: the outputs that are there now, the default one ticked.
        var sound = new ToolStripMenuItem(I18n.L("צליל", "Sound"));
        try
        {
            foreach (var o in AudioDevices.List().Outputs.Where(o => o.Active))
            {
                var id = o.Id;
                sound.DropDownItems.Add(new ToolStripMenuItem(o.Name, null, async (_, _) =>
                {
                    try { await Task.Run(() => AudioDevices.SetDefault(id, settings.SwitchCalls)); PostAudio(); }
                    catch (Exception ex) { Log.Error("tray sound", ex); }
                }) { Checked = o.Default });
            }
        }
        catch (Exception ex) { Log.Error("tray sound list", ex); }
        menu.Items.Add(sound);

        var lighting = DeviceControls.Lighting;
        var effects = new ToolStripMenuItem(I18n.L("תאורת המקלדת", "Keyboard lighting"));
        effects.DropDownItems.Add(new ToolStripMenuItem(I18n.L("של המקלדת (כבוי)", "Keyboard's own (off)"), null, (_, _) => TrayChange(() => DeviceControls.SetEffect("off"))) { Checked = !lighting.Enabled });
        foreach (var (key, he, en) in EffectNames)
            effects.DropDownItems.Add(new ToolStripMenuItem(I18n.L(he, en), null, (_, _) => TrayChange(() => DeviceControls.SetEffect(key))) { Checked = lighting.Enabled && lighting.Effect == key });
        menu.Items.Add(effects);

        var current = DeviceControls.Screen.Mode;
        var screens = new ToolStripMenuItem(I18n.L("המסך במקלדת", "Keyboard screen"));
        foreach (var (key, he, en) in ScreenNames)
            screens.DropDownItems.Add(new ToolStripMenuItem(I18n.L(he, en), null, (_, _) => TrayChange(() => DeviceControls.SetScreen(key))) { Checked = current == key });
        menu.Items.Add(screens);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(I18n.L("פתח את DeviceHub", "Open DeviceHub"), null, (_, _) => ShowFromTray());
        menu.Items.Add(I18n.L("יציאה (האפקטים ייעצרו)", "Exit (effects stop)"), null, (_, _) => { exiting = true; Close(); });
    }

    /// <summary>A change from the tray menu, then the open page brought up to date.</summary>
    private async void TrayChange(Action change)
    {
        try
        {
            change();
            if (pageReady && Visible) Post("deviceSettings", new { device = "keyboard", settings = await DeviceControls.ReadSettings("keyboard") });
        }
        catch (Exception ex) { Log.Error("tray", ex); }
    }

    private static readonly (string Key, string He, string En)[] EffectNames =
    [
        ("static", "סטטי", "Static"), ("breathing", "נשימה", "Breathing"), ("wave", "גל", "Wave"), ("spectrum", "ספקטרום", "Spectrum"),
        ("gradient", "מעבר צבע", "Gradient"), ("aurora", "זוהר", "Aurora"), ("starlight", "כוכבים", "Starlight"), ("rain", "גשם", "Rain"),
        ("fire", "אש", "Fire"), ("music", "מוזיקה", "Music"), ("visualizer", "מד צליל", "Sound meter"), ("temperature", "חום המעבד", "CPU heat"),
        ("reactive", "לחיצה", "Key press"), ("ripple", "אדווה", "Ripple"),
    ];

    private static readonly (string Key, string He, string En)[] ScreenNames =
    [
        ("off", "כבוי", "Off"), ("clock", "שעון", "Clock"), ("analog", "שעון מחוגים", "Analog clock"), ("devices", "סוללות", "Batteries"),
        ("system", "מחשב", "System"), ("music", "מה מתנגן", "Now playing"), ("cycle", "סבב", "Rotation"), ("text", "טקסט", "Text"),
        ("image", "תמונה", "Picture"), ("show", "מופע", "Show"), ("stars", "מהירות האור", "Warp speed"), ("matrix", "מטריקס", "Matrix"),
        ("energy", "כדור אנרגיה", "Energy ball"), ("power", "עלייה בכוח", "Power up"), ("fireworks", "זיקוקים", "Fireworks"),
        ("life", "משחק החיים", "Game of Life"), ("bounce", "לוגו קופץ", "Bouncing logo"), ("sound", "פסי מוזיקה", "Music bars"), ("goal", "גול!", "Goal!"),
    ];

    private void HideToTray()
    {
        Hide();
        sensorTimer.Stop();
        deviceTimer.Stop();
        deviceTimer.Interval = 120_000;
        deviceTimer.Start();
        watchScreen = false;
        _ = SuspendPageAsync();
    }

    private void ShowFromTray()
    {
        if (IsDisposed) return;
        var wasHidden = !Visible;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        if (wasHidden && pageReady) { web.Visible = true; RefreshAll(); }
    }

    private async Task SuspendPageAsync()
    {
        if (web.CoreWebView2 is null) return;
        web.Visible = false;
        try { await web.CoreWebView2.TrySuspendAsync(); }
        catch (InvalidOperationException) { } // already suspended, or busy with a dialog
    }

    private async Task InitWebAsync()
    {
        // DEVICEHUB_WEBVIEW_DIR lets a development copy run next to an installed one (WebView2 locks its data folder).
        var webData = Environment.GetEnvironmentVariable("DEVICEHUB_WEBVIEW_DIR") is { Length: > 0 } dir ? dir : Path.Combine(AppSettings.DataDir, "WebView2");
        var env = await CoreWebView2Environment.CreateAsync(null, webData);
        await web.EnsureCoreWebView2Async(env);

        var core = web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        // An app, not a browser: no reload, find or print shortcuts.
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Deny);
        core.WebMessageReceived += OnMessage;
        core.Navigate($"https://{Host}/index.html");

        try { audioWatcher = new AudioWatcher(() => OnUiThread(() => { audioDebounce.Stop(); audioDebounce.Start(); })); }
        catch (Exception ex) { Debug.WriteLine($"audio watcher: {ex}"); } // the list still refreshes on demand
    }

    /// <summary>Minimized: stop reading and suspend the page. Restored: read everything once, then carry on.</summary>
    private async Task OnResizedAsync()
    {
        if (web.CoreWebView2 is null) return;
        if (WindowState == FormWindowState.Minimized)
        {
            if (settings.MinimizeToTray)
            {
                // Like Windows Security: minimized means into the tray, next to the clock.
                HideToTray();
                WindowState = FormWindowState.Normal; // hidden, so this only decides how it comes back
                return;
            }
            sensorTimer.Stop();
            deviceTimer.Stop();
            await SuspendPageAsync();
        }
        else if (!web.Visible && Visible)
        {
            web.Visible = true; // visibility resumes a suspended WebView2
            if (pageReady) RefreshAll();
        }
    }

    /// <summary>A key-press effect remembered from last time listens from the start, tray or not.</summary>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (LightingEngine.Instance.KeysWanted) RawKeys.Listen(Handle, true);
    }

    /// <summary>Plugging a USB device in or out (a dongle, the controller): read the devices again once things settle.</summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == RawKeys.WmInput && RawKeys.KeyDown(m.LParam) is { } key) LightingEngine.Instance.KeyPressed(key);
        if (m.Msg == WmDeviceChange)
        {
            LightingEngine.Instance.DevicesChanged();
            if (pageReady && Visible && WindowState != FormWindowState.Minimized)
            {
                plugDebounce.Stop();
                plugDebounce.Start();
            }
        }
        base.WndProc(ref m);
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string cmd;
        JsonElement msg;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            msg = doc.RootElement.Clone();
            cmd = msg.GetProperty("cmd").GetString() ?? "";
        }
        catch (Exception)
        {
            return;
        }

        try
        {
            switch (cmd)
            {
                case "hello":
                    Post("hello", new { settings, startWithWindows = Startup.Enabled, version = Application.ProductVersion.Split('+')[0] });
                    pageReady = true;
                    RefreshAll();
                    break;

                case "refresh":
                    RefreshAll();
                    break;

                case "setAudio":
                    var id = msg.GetProperty("id").GetString() ?? "";
                    var calls = settings.SwitchCalls;
                    await Task.Run(() => AudioDevices.SetDefault(id, calls));
                    PostAudio(); // the watcher will post again; this one makes the press feel instant
                    break;

                case "settingsFor":
                    var forDevice = msg.GetProperty("device").GetString() ?? "";
                    Post("deviceSettings", new { device = forDevice, settings = await DeviceControls.ReadSettings(forDevice) });
                    break;

                case "health":
                    // How DeviceHub itself is doing, for the health section in Settings.
                    using (var self = Process.GetCurrentProcess())
                    {
                        var up = DateTime.Now - self.StartTime;
                        var engine = LightingEngine.Instance;
                        Post("health", new
                        {
                            started = self.StartTime,
                            hours = Math.Round(up.TotalHours, 1),
                            cpu = Math.Round(self.TotalProcessorTime.TotalSeconds / Math.Max(1, up.TotalSeconds) * 100 / Environment.ProcessorCount, 3),
                            memory = self.WorkingSet64 / (1024 * 1024),
                            keyFrames = engine.KeyFrames, screenFrames = engine.ScreenFrames, padFrames = engine.PadFrames,
                            keyboard = engine.KeyboardConnected,
                            errors = Log.Errors, lastError = Log.LastError,
                            startup = Startup.Enabled,
                        });
                    }
                    break;

                case "openLog":
                    if (File.Exists(Log.FilePath)) Process.Start("explorer.exe", $"/select,\"{Log.FilePath}\"")?.Dispose();
                    break;

                case "watchScreen":
                    // The keyboard page shows what the screen shows, live, only while it is open.
                    watchScreen = msg.GetProperty("on").GetBoolean();
                    if (watchScreen && LightingEngine.Instance.CurrentScreen is { } now) Post("screen", Convert.ToBase64String(now));
                    break;

                case "control":
                    var device = msg.GetProperty("device").GetString() ?? "";
                    var action = msg.GetProperty("action").GetString() ?? "";
                    try
                    {
                        var after = await DeviceControls.Apply(device, action, msg.GetProperty("value").Clone());
                        Post("deviceSettings", new { device, settings = after });
                        Post("controlDone", new { device, action });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"control {device}/{action}: {ex}");
                        Post("error", new { cmd = "control", device, action, message = ex.Message });
                    }
                    break;

                case "saveSettings":
                    var languageBefore = settings.Language;
                    settings = JsonSerializer.Deserialize<AppSettings>(msg.GetProperty("settings").GetRawText(), Program.Json) ?? settings;
                    settings.Save();
                    I18n.English = settings.Language == "en";
                    LightingEngine.Instance.English = I18n.English;
                    if (msg.TryGetProperty("startWithWindows", out var sw)) Startup.Set(sw.GetBoolean());
                    Post("settings", new { settings, startWithWindows = Startup.Enabled });
                    if (settings.Language != languageBefore) { PostAudio(); BuildTrayMenu(); } // device names come from here, in the chosen language
                    break;
            }
        }
        catch (Exception ex)
        {
            ReportError(cmd, ex);
        }
    }

    private void RefreshAll()
    {
        PostAudio();
        ReadDevices();
        ReadSensors();
        sensorTimer.Start();
        deviceTimer.Stop();
        deviceTimer.Interval = 60_000;
        deviceTimer.Start();
    }

    private async void ReadDevices()
    {
        if (readingDevices || IsDisposed) return;
        readingDevices = true;
        try
        {
            Post("devicesBusy", new { });
            var list = await DeviceReader.ReadAllAsync();
            lastDevices = list;
            lastLaptop = list.FirstOrDefault(d => d.Id == "laptop") ?? lastLaptop;
            Post("devices", list);
            UpdateTray(list);
        }
        catch (Exception ex) { ReportError("devices", ex); }
        finally { readingDevices = false; }
    }

    /// <summary>The tray's tooltip shows every battery; a device dropping below 15 % gets one note until it recovers.</summary>
    private void UpdateTray(List<DeviceReading> list)
    {
        var parts = list.Where(d => d.Battery is not null && d.State == DeviceState.Ready)
            .Select(d => $"{Short(d.Id)} {d.Battery}%{(d.Charging ? "+" : "")}");
        var tip = "DeviceHub\n" + string.Join("  ", parts);
        tray.Text = tip.Length > 127 ? tip[..127] : tip;

        foreach (var d in list.Where(d => d.Battery is not null))
        {
            if (d.Battery >= 25 || d.Charging) { warned.Remove(d.Id); continue; }
            if (d.Battery > 15 || !settings.BatteryAlerts || !warned.Add(d.Id)) continue;
            tray.ShowBalloonTip(8000, I18n.L("סוללה חלשה", "Battery low"),
                I18n.L($"{Name(d.Id)}: נשארו {d.Battery}%", $"{Name(d.Id)}: {d.Battery}% left"), ToolTipIcon.Warning);
        }
    }

    private static string Short(string id) => id switch { "mouse" => I18n.L("עכבר", "Mouse"), "headset" => I18n.L("אוזניות", "Headset"), "controller" => I18n.L("שלט", "Pad"), _ => id };
    private static string Name(string id) => id switch { "mouse" => "Razer Basilisk X HyperSpeed", "headset" => "Arctis 7+", "controller" => "DualSense Edge", _ => id };

    private async void ReadSensors()
    {
        if (readingSensors || IsDisposed) return;
        readingSensors = true;
        try { lastLaptop = await Task.Run(DeviceReader.ReadLaptop); Post("laptop", lastLaptop); }
        catch (Exception ex) { ReportError("laptop", ex); }
        finally { readingSensors = false; }
    }

    private async void PostAudio()
    {
        try { Post("audio", await Task.Run(AudioDevices.List)); }
        catch (Exception ex) { ReportError("audio", ex); }
    }

    private void ReportError(string cmd, Exception error)
    {
        Log.Error(cmd, error);
        try { Post("error", new { cmd, message = error.Message }); }
        catch (Exception postError) { Debug.WriteLine(postError); } // WebView2 may already be closing; never escape an async event handler.
    }

    /// <summary>To the page, while it is there to listen (not in the tray, not minimized).</summary>
    private void Post(string type, object data)
    {
        if (IsDisposed || web.CoreWebView2 is null || !web.Visible) return;
        web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, data }, Program.Json));
    }

    private void OnUiThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { } // window closing
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { tray.Dispose(); showSignal.Dispose(); }
        base.Dispose(disposing);
    }
}
