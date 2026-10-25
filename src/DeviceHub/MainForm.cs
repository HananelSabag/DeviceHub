using System.Diagnostics;
using System.Text.Json;
using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DeviceHub;

/// <summary>
/// The host window: a WebView2 showing wwwroot. The page talks to us with chrome.webview.postMessage({ cmd })
/// and we answer with { type, data }.
/// While the window is visible the laptop sensors update every 3 seconds and the wireless devices every minute
/// (and right away when something is plugged in or out). Minimized, nothing is read and the page is suspended.
/// Sound devices are never polled: Windows tells us when they change.
/// </summary>
internal sealed class MainForm : Form
{
    private const string Host = "devicehub.example";
    private const int WmDeviceChange = 0x0219;
    private static readonly Color Backdrop = Color.FromArgb(26, 29, 35);

    private readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Backdrop };
    private readonly System.Windows.Forms.Timer sensorTimer = new() { Interval = 3000 };
    private readonly System.Windows.Forms.Timer deviceTimer = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer plugDebounce = new() { Interval = 1500 };
    private readonly System.Windows.Forms.Timer audioDebounce = new() { Interval = 250 };
    private AppSettings settings = AppSettings.Load();
    private AudioWatcher? audioWatcher;
    private bool pageReady;
    private bool readingDevices;
    private bool readingSensors;

    public MainForm()
    {
        I18n.English = settings.Language == "en";
        Text = "DeviceHub";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        BackColor = Backdrop;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = LogicalToDeviceUnits(new Size(980, 640));
        Size = LogicalToDeviceUnits(new Size(1280, 800));
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(web);

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
        FormClosing += (_, _) =>
        {
            sensorTimer.Stop();
            deviceTimer.Stop();
            audioWatcher?.Dispose();
        };
        sensorTimer.Tick += (_, _) => ReadSensors();
        deviceTimer.Tick += (_, _) => ReadDevices();
        plugDebounce.Tick += (_, _) => { plugDebounce.Stop(); ReadDevices(); };
        audioDebounce.Tick += (_, _) => { audioDebounce.Stop(); PostAudio(); };
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
            sensorTimer.Stop();
            deviceTimer.Stop();
            web.Visible = false;
            try { await web.CoreWebView2.TrySuspendAsync(); }
            catch (InvalidOperationException) { } // already suspended, or busy with a dialog
        }
        else if (!web.Visible)
        {
            web.Visible = true; // visibility resumes a suspended WebView2
            if (pageReady) RefreshAll();
        }
    }

    /// <summary>Plugging a USB device in or out (a dongle, the controller): read the devices again once things settle.</summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDeviceChange && pageReady && WindowState != FormWindowState.Minimized)
        {
            plugDebounce.Stop();
            plugDebounce.Start();
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
                    Post("hello", new { settings, version = Application.ProductVersion.Split('+')[0] });
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
                    Post("settings", settings);
                    if (settings.Language != languageBefore) PostAudio(); // device names come from here, in the chosen language
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
        deviceTimer.Start();
    }

    private async void ReadDevices()
    {
        if (readingDevices || IsDisposed) return;
        readingDevices = true;
        try
        {
            Post("devicesBusy", new { });
            Post("devices", await DeviceReader.ReadAllAsync());
        }
        catch (Exception ex) { ReportError("devices", ex); }
        finally { readingDevices = false; }
    }

    private async void ReadSensors()
    {
        if (readingSensors || IsDisposed) return;
        readingSensors = true;
        try { Post("laptop", await Task.Run(DeviceReader.ReadLaptop)); }
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
        Debug.WriteLine($"{cmd}: {error}");
        try { Post("error", new { cmd, message = error.Message }); }
        catch (Exception postError) { Debug.WriteLine(postError); } // WebView2 may already be closing; never escape an async event handler.
    }

    private void Post(string type, object data)
    {
        if (IsDisposed || web.CoreWebView2 is null) return;
        web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type, data }, Program.Json));
    }

    private void OnUiThread(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { } // window closing
    }
}
