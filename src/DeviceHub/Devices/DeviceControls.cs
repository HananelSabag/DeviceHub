using System.Text.Json;
using DeviceHub.Core;
using DeviceHub.Interop;
using DeviceHub.Lighting;

namespace DeviceHub.Devices;

/// <summary>
/// What DeviceHub has to remember itself: the headset can't report its sidetone, auto-off or equalizer back,
/// and the controller's and keyboard's lights and the keyboard's screen are forgotten when they lose power.
/// </summary>
internal sealed record ControlState
{
    public int? Sidetone { get; init; }
    public int? AutoOff { get; init; }
    public string? EqPreset { get; init; }
    public double[]? Eq { get; init; }
    public DualSense.Lights? ControllerLights { get; init; }
    public KeyboardLighting? Lighting { get; init; }
    public OledSettings? Screen { get; init; }

    private static string FilePath => Path.Combine(AppSettings.DataDir, "devices.json");
    public static string PicturePath => Path.Combine(AppSettings.DataDir, "screen-picture");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static ControlState Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<ControlState>(File.ReadAllText(FilePath), Json) ?? new() : new(); }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppSettings.DataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }
}

/// <summary>Every change the page can ask for, one device at a time, never while another read or write is running.</summary>
internal static class DeviceControls
{
    private static ControlState state = ControlState.Load();
    private static readonly HashSet<string> lit = []; // controller paths that already got their lights since they were plugged in

    /// <summary>Hands the remembered keyboard lighting and screen to the engine and starts it.</summary>
    public static void StartLighting(bool english)
    {
        var engine = LightingEngine.Instance;
        engine.English = english;
        engine.Configure(state.Lighting ?? new(), state.Screen ?? new(), LoadPicture());
        engine.Start();
    }

    private static List<(byte[], int)>? LoadPicture()
    {
        try { return File.Exists(ControlState.PicturePath) ? OledScreen.Picture(File.ReadAllBytes(ControlState.PicturePath)) : null; }
        catch { return null; } // a broken picture file must not stop the rest
    }

    public static async Task<object?> ReadSettings(string device)
    {
        if (device == "keyboard") return KeyboardSettings();
        await DeviceReader.Gate.WaitAsync();
        try
        {
            var hid = Hid.Enumerate(RazerMouse.Vendor, ArctisHeadset.Vendor, DualSense.Vendor);
            return device switch
            {
                "mouse" => await Task.Run(() => RazerMouse.ReadSettings(hid)),
                "headset" => new { state.Sidetone, state.AutoOff, eqPreset = state.EqPreset ?? (state.Eq is null ? null : "custom"), eq = state.Eq, presets = ArctisHeadset.Presets },
                "controller" => new { lights = state.ControllerLights },
                _ => null,
            };
        }
        finally { DeviceReader.Gate.Release(); }
    }

    /// <summary>The keyboard page: lighting, screen, and the key map the preview is drawn from.</summary>
    private static object KeyboardSettings() => new
    {
        lighting = state.Lighting ?? new KeyboardLighting(),
        screen = state.Screen ?? new OledSettings(),
        hasPicture = File.Exists(ControlState.PicturePath),
        layout = new { width = KeyboardLayout.Width, height = KeyboardLayout.Height, keys = KeyboardLayout.Keys.Select(k => new[] { k.Code, k.X, k.Y, k.W, k.H }), accent = KeyboardLayout.Accent },
    };

    /// <summary>Applies one change and returns the device's settings as they are now.</summary>
    public static async Task<object?> Apply(string device, string action, JsonElement value)
    {
        if (device == "keyboard") { ApplyKeyboard(action, value); return KeyboardSettings(); }
        await DeviceReader.Gate.WaitAsync();
        try
        {
            var hid = Hid.Enumerate(RazerMouse.Vendor, ArctisHeadset.Vendor, DualSense.Vendor);
            switch (device, action)
            {
                case ("mouse", "polling"): await Task.Run(() => RazerMouse.SetPolling(hid, value.GetInt32())); break;
                case ("mouse", "dpi"): await Task.Run(() => RazerMouse.SetDpi(hid, value.GetInt32())); break;
                case ("mouse", "stages"):
                    var stages = value.GetProperty("stages").EnumerateArray().Select(x => x.GetInt32()).ToList();
                    var active = value.GetProperty("active").GetInt32();
                    await Task.Run(() => RazerMouse.SetStages(hid, stages, active));
                    break;
                case ("mouse", "sleep"): await Task.Run(() => RazerMouse.SetSleep(hid, value.GetInt32())); break;
                case ("mouse", "lowBattery"): await Task.Run(() => RazerMouse.SetLowBattery(hid, value.GetInt32())); break;

                case ("headset", "sidetone"):
                    await ArctisHeadset.SetSidetone(hid, value.GetInt32());
                    Remember(state with { Sidetone = value.GetInt32() });
                    break;
                case ("headset", "autoOff"):
                    await ArctisHeadset.SetAutoOff(hid, value.GetInt32());
                    Remember(state with { AutoOff = value.GetInt32() });
                    break;
                case ("headset", "eqPreset"):
                    var name = value.GetString() ?? "";
                    var bands = ArctisHeadset.Presets.GetValueOrDefault(name) ?? throw new ArgumentException($"no preset {name}");
                    await ArctisHeadset.SetEqualizer(hid, bands);
                    Remember(state with { EqPreset = name, Eq = bands });
                    break;
                case ("headset", "eq"):
                    var custom = value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
                    await ArctisHeadset.SetEqualizer(hid, custom);
                    Remember(state with { EqPreset = "custom", Eq = custom });
                    break;

                case ("controller", "lights"):
                    var lights = value.Deserialize<DualSense.Lights>(Program.Json) ?? throw new ArgumentException("lights");
                    await DualSense.SetLights(hid, lights);
                    Remember(state with { ControllerLights = lights });
                    break;

                default: throw new ArgumentException($"unknown control {device}/{action}");
            }
        }
        finally { DeviceReader.Gate.Release(); }
        return await ReadSettings(device);
    }

    /// <summary>The keyboard is driven by the lighting engine: a change is remembered and handed over, never sent from here.</summary>
    private static void ApplyKeyboard(string action, JsonElement value)
    {
        var engine = LightingEngine.Instance;
        List<(byte[], int)>? picture = null;
        switch (action)
        {
            case "lighting":
                Remember(state with { Lighting = value.Deserialize<KeyboardLighting>(Program.Json) ?? throw new ArgumentException("lighting") });
                break;
            case "screen":
                Remember(state with { Screen = value.Deserialize<OledSettings>(Program.Json) ?? throw new ArgumentException("screen") });
                break;
            case "picture":
                // A data URL from the page's file picker: decoded and dithered once here, kept as the original file.
                var url = value.GetString() ?? "";
                var bytes = Convert.FromBase64String(url[(url.IndexOf(',') + 1)..]);
                if (bytes.Length > 8 * 1024 * 1024) throw new ArgumentException("the picture is larger than 8 MB");
                picture = OledScreen.Picture(bytes);
                Directory.CreateDirectory(AppSettings.DataDir);
                File.WriteAllBytes(ControlState.PicturePath, bytes);
                Remember(state with { Screen = (state.Screen ?? new()) with { Mode = "image" } });
                break;
            default: throw new ArgumentException($"unknown control keyboard/{action}");
        }
        engine.Configure(state.Lighting ?? new(), state.Screen ?? new(), picture);
    }

    /// <summary>
    /// The controller's lights are lost with power: sent again once each time it is plugged in.
    /// Called with the HID list of every device read. (The keyboard is the lighting engine's job.)
    /// </summary>
    public static async Task ReapplyAsync(IReadOnlyList<HidInfo> hid)
    {
        var present = hid.Select(h => h.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lit.RemoveWhere(p => !present.Contains(p)); // unplugged: it forgot, so send again next time

        if (state.ControllerLights is { } lights
            && hid.FirstOrDefault(x => x.Vid == DualSense.Vendor && x.Pid is DualSense.DualSensePid or DualSense.EdgePid && x.UsagePage == 0x01 && x.Usage == 0x05 && !x.Bluetooth) is { } pad
            && lit.Add(pad.Path))
        {
            try { await DualSense.SetLights(hid, lights); }
            catch (IOException) { lit.Remove(pad.Path); }
        }
    }

    private static void Remember(ControlState next)
    {
        state = next;
        state.Save();
    }
}
