using System.Text.Json;
using DeviceHub.Core;
using DeviceHub.Interop;

namespace DeviceHub.Devices;

/// <summary>
/// What DeviceHub has to remember itself: the headset can't report its sidetone, auto-off or equalizer back,
/// and the controller's and keyboard's lights are forgotten when they lose power.
/// </summary>
internal sealed record ControlState
{
    public int? Sidetone { get; init; }
    public int? AutoOff { get; init; }
    public string? EqPreset { get; init; }
    public double[]? Eq { get; init; }
    public DualSense.Lights? ControllerLights { get; init; }
    public KeyboardLighting? Keyboard { get; init; }

    private static string FilePath => Path.Combine(AppSettings.DataDir, "devices.json");
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

/// <summary>own: the keyboard's saved profile. color: DeviceHub's colour on every key, an optional accent, 0-100 %.</summary>
internal sealed record KeyboardLighting(string Mode, ApexKeyboard.Rgb Main, ApexKeyboard.Rgb? Accent, int Brightness = 100);

/// <summary>Every change the page can ask for, one device at a time, never while another read or write is running.</summary>
internal static class DeviceControls
{
    private static ControlState state = ControlState.Load();
    private static readonly HashSet<string> lit = []; // device paths that already got their lights since they were plugged in

    public static async Task<object?> ReadSettings(string device)
    {
        await DeviceReader.Gate.WaitAsync();
        try
        {
            var hid = Hid.Enumerate(RazerMouse.Vendor, ArctisHeadset.Vendor, DualSense.Vendor);
            return device switch
            {
                "mouse" => await Task.Run(() => RazerMouse.ReadSettings(hid)),
                "headset" => new { state.Sidetone, state.AutoOff, eqPreset = state.EqPreset ?? (state.Eq is null ? null : "custom"), eq = state.Eq, presets = ArctisHeadset.Presets },
                "controller" => new { lights = state.ControllerLights },
                "keyboard" => new { lighting = state.Keyboard },
                _ => null,
            };
        }
        finally { DeviceReader.Gate.Release(); }
    }

    /// <summary>Applies one change and returns the device's settings as they are now.</summary>
    public static async Task<object?> Apply(string device, string action, JsonElement value)
    {
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

                case ("keyboard", "lighting"):
                    var k = value.Deserialize<KeyboardLighting>(Program.Json) ?? throw new ArgumentException("lighting");
                    if (k.Mode == "own") await ApexKeyboard.UseOwnLighting(hid);
                    else await ApexKeyboard.SetColors(hid, k.Main, k.Accent, k.Brightness);
                    Remember(state with { Keyboard = k });
                    break;

                default: throw new ArgumentException($"unknown control {device}/{action}");
            }
        }
        finally { DeviceReader.Gate.Release(); }
        return await ReadSettings(device);
    }

    /// <summary>
    /// Lights the controller and keyboard lose with power: sent again once each time they are plugged in.
    /// Called with the HID list of every device read; does nothing for devices that already have them.
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

        if (state.Keyboard is { Mode: "color" } k
            && hid.FirstOrDefault(x => x.Vid == ApexKeyboard.Vendor && x.Pid == ApexKeyboard.Apex5 && x.UsagePage == 0xFFC0 && x.Usage == 0x01) is { } kb
            && lit.Add(kb.Path))
        {
            try { await ApexKeyboard.SetColors(hid, k.Main, k.Accent, k.Brightness); }
            catch (IOException) { lit.Remove(kb.Path); }
        }
    }

    private static void Remember(ControlState next)
    {
        state = next;
        state.Save();
    }
}
