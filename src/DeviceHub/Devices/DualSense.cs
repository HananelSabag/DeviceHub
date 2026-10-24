using DeviceHub.Interop;

namespace DeviceHub.Devices;

/// <summary>
/// DualSense and DualSense Edge. The battery is part of every input report the controller sends anyway,
/// so this only opens the gamepad collection for reading and looks at one report. Nothing is sent.
/// </summary>
internal static class DualSense
{
    public const ushort Vendor = 0x054C, DualSensePid = 0x0CE6, EdgePid = 0x0DF2;

    public static async Task<DeviceReading> ReadAsync(IReadOnlyList<HidInfo> hid)
    {
        var d = hid.FirstOrDefault(x => x.Vid == Vendor && x.Pid is DualSensePid or EdgePid && x.UsagePage == 0x01 && x.Usage == 0x05);
        var model = d?.Pid == DualSensePid ? "DualSense" : "DualSense Edge";
        if (d is null) return new() { Id = "controller", Model = model, State = DeviceState.Missing };
        var link = d.Bluetooth ? "bluetooth" : "usb";
        await using var fs = Hid.OpenStream(d.Path, write: false);
        if (fs is null) return new() { Id = "controller", Model = model, State = DeviceState.Error, Link = link, Detail = "cannot open the controller" };

        var res = await Hid.ReadAsync(fs, d.InputLength, 1000);
        if (res is null) return new() { Id = "controller", Model = model, State = DeviceState.Error, Link = link, Detail = "no input report within 1 s" };
        if (Parse(res) is not { } s)
            // Over Bluetooth the controller sends a short report until a game asks for the full one; that short one has no battery.
            return new() { Id = "controller", Model = model, State = DeviceState.Limited, Link = link, Detail = $"report 0x{res[0]:X2} has no battery" };
        return new() { Id = "controller", Model = model, State = DeviceState.Ready, Link = link, Battery = s.Battery, Charging = s.Charging, Full = s.Full };
    }

    internal sealed record Status(int Battery, bool Charging, bool Full);

    /// <summary>
    /// USB report 0x01: the status byte is at 53. Bluetooth's full report 0x31 has one more byte in front: 54.
    /// Low nibble: level 0-10. High nibble: 0 on battery, 1 charging, 2 full (the Linux hid-playstation layout).
    /// </summary>
    internal static Status? Parse(byte[] r)
    {
        int at;
        if (r.Length >= 64 && r[0] == 0x01) at = 53;
        else if (r.Length >= 78 && r[0] == 0x31) at = 54;
        else return null;
        var b = r[at];
        var state = b >> 4;
        var level = Math.Min(100, (b & 0x0F) * 10 + 5);
        return state switch
        {
            0x2 => new Status(100, false, true),
            0x1 => new Status(level, true, false),
            _ => new Status(level, false, false),
        };
    }
}
