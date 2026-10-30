using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;
using DeviceHub.Lighting;

namespace DeviceHub;

internal static class Program
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Hebrew readable in dumps
        WriteIndented = true,
    };

    /// <summary>
    /// DeviceHub.exe                              → the app (a second start opens the first one's window)
    /// DeviceHub.exe --tray                       → start quietly in the tray (how Windows starts it)
    /// DeviceHub.exe --screen-preview clock out.png → the keyboard screen's picture, 4× larger
    /// DeviceHub.exe --dump-json out.json [en]    → read every device and the sound devices once, save them (read-only)
    /// DeviceHub.exe --audio-switch-test out.json → switch the default output to another device and straight back
    /// DeviceHub.exe --settings mouse out.json    → a device page's settings
    /// DeviceHub.exe --control mouse dpi 1600 out.json → one change, then the settings read back
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--dump-json", var dumpPath, ..])
        {
            I18n.English = args.Length > 2 && args[2] == "en";
            var devices = DeviceReader.ReadAllAsync().GetAwaiter().GetResult();
            File.WriteAllText(dumpPath, JsonSerializer.Serialize(new { devices, audio = AudioDevices.List() }, Json));
            return 0;
        }

        // A device page's settings, as the page would get them.
        if (args is ["--settings", var settingsDevice, var settingsPath, ..])
        {
            var current = DeviceControls.ReadSettings(settingsDevice).GetAwaiter().GetResult();
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(current, Json));
            return 0;
        }

        // One change from a script: --control mouse polling 1000 out.json (the value is JSON).
        if (args is ["--control", var device, var action, var valueJson, var controlPath, ..])
        {
            try
            {
                using var value = JsonDocument.Parse(valueJson);
                var after = DeviceControls.Apply(device, action, value.RootElement.Clone()).GetAwaiter().GetResult();
                File.WriteAllText(controlPath, JsonSerializer.Serialize(new { ok = true, after }, Json));
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(controlPath, JsonSerializer.Serialize(new { ok = false, error = ex.Message }, Json));
                return 1;
            }
        }

        if (args is ["--audio-switch-test", var testPath, ..])
        {
            var result = AudioSwitchTest();
            File.WriteAllText(testPath, JsonSerializer.Serialize(result, Json));
            return result.Ok ? 0 : 1;
        }

        // How the keyboard's screen looks, as a picture: --screen-preview clock out.png [text]
        if (args is ["--screen-preview", var mode, var previewPath, ..])
        {
            var devices = mode == "devices" ? DeviceReader.ReadAllAsync().GetAwaiter().GetResult() : [];
            var track = mode == "music" ? NowPlaying.GetAsync().GetAwaiter().GetResult() : null;
            var bits = mode switch
            {
                "devices" => OledScreen.Devices(devices, (0, 0)),
                "music" when track is not null => OledScreen.Music(track, 0),
                "text" => OledScreen.Text(args.Length > 3 ? args[3] : "", (0, 0)),
                "image" => OledScreen.Picture(File.ReadAllBytes(args[3]))[0].Bits,
                _ => OledScreen.Clock(DateTime.Now, args.Length > 3 && args[3] == "en"),
            };
            using var bmp = OledScreen.ToBitmap(bits);
            bmp.Save(previewPath, System.Drawing.Imaging.ImageFormat.Png);
            if (Environment.GetEnvironmentVariable("DEVICEHUB_SEND") == "1"
                && ApexKeyboard.Find(Interop.Hid.Enumerate(ApexKeyboard.Vendor)) is { } kb && ApexKeyboard.Open(kb) is { } h)
                using (h) ApexKeyboard.SendScreen(h, bits); // and on the keyboard itself, for checking by eye
            return 0;
        }

        // An animation on the keyboard for a while, and its frames as one picture: --screen-demo energy 12 sheet.png
        if (args is ["--screen-demo", var demoMode, var secondsText, var sheetPath, ..] && ScreenAnimation.For(demoMode) is { } demo)
        {
            var kb = ApexKeyboard.Find(Interop.Hid.Enumerate(ApexKeyboard.Vendor));
            using var h = kb is null ? null : ApexKeyboard.Open(kb);
            var level = new Audio.SoundLevel();
            var shots = new List<byte[]>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var total = double.Parse(secondsText, CultureInfo.InvariantCulture);
            while (watch.Elapsed.TotalSeconds < total)
            {
                var t = watch.Elapsed.TotalSeconds;
                var bits = demo.Frame(t, level.Read());
                if (h is not null) ApexKeyboard.SendScreen(h, bits);
                if (shots.Count < 12 && t >= shots.Count * Math.Min(total, 5) / 12) shots.Add(bits);
                Thread.Sleep(50);
            }
            using var sheet = new System.Drawing.Bitmap(128 * 3 * 2 + 12, (40 * 3 + 6) * 6);
            using (var g = System.Drawing.Graphics.FromImage(sheet))
            {
                g.Clear(System.Drawing.Color.FromArgb(60, 60, 60));
                for (var i = 0; i < shots.Count; i++)
                {
                    using var shot = OledScreen.ToBitmap(shots[i], 3);
                    g.DrawImage(shot, (i % 2) * (128 * 3 + 12), (i / 2) * (40 * 3 + 6));
                }
            }
            sheet.Save(sheetPath, System.Drawing.Imaging.ImageFormat.Png);
            return 0;
        }

        // One DeviceHub at a time: a second start (the shortcut, while it sits in the tray) opens the first one's window.
        using var single = new Mutex(true, "DeviceHub.Single", out var first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting(MainForm.ShowSignal).Set(); } catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }

        // Whatever happens, the log says it: a DeviceHub that stopped can tell why next time.
        Log.Info($"DeviceHub {Application.ProductVersion.Split('+')[0]} started{(args.Length > 0 ? " " + string.Join(' ', args) : "")}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("crash", e.ExceptionObject as Exception ?? new Exception("unknown"));
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("background task", e.Exception.GetBaseException()); e.SetObserved(); };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("window", e.Exception); // logged and carried on, never a dialog
#if !DEBUG
        Startup.Tidy();
#endif

        ApplicationConfiguration.Initialize();
        var settings = AppSettings.Load();
        DeviceControls.StartLighting(settings.Language == "en");
        Application.Run(new MainForm(startHidden: args.Contains("--tray")));
        LightingEngine.Instance.Dispose();
        Log.Info("DeviceHub ended");
        return 0;
    }

    private sealed record SwitchTest(bool Ok, string? Message, Dictionary<Role, string?> Before, string? Target, Dictionary<Role, string?>? During, Dictionary<Role, string?>? After);

    /// <summary>Proves switching works on this PC: one other active output for a moment, then every role exactly as it was.</summary>
    private static SwitchTest AudioSwitchTest()
    {
        I18n.English = true;
        var before = AudioDevices.Defaults(DataFlow.Render);
        var target = AudioDevices.List().Outputs.FirstOrDefault(o => o.Active && o.Id != before[Role.Console]);
        if (target is null) return new(false, "only one active output, nothing to switch to", before, null, null, null);
        AudioDevices.SetDefault(target.Id, calls: false);
        var during = AudioDevices.Defaults(DataFlow.Render);
        foreach (var (role, id) in before)
            if (id is not null) AudioDevices.SetDefault(id, role);
        var after = AudioDevices.Defaults(DataFlow.Render);
        var ok = during[Role.Console] == target.Id && during[Role.Multimedia] == target.Id && before.All(kv => after[kv.Key] == kv.Value);
        return new(ok, ok ? $"switched to {target.Name} and back" : "the default did not follow", before, target.Id, during, after);
    }
}
