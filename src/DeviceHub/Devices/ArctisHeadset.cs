using DeviceHub.Interop;

namespace DeviceHub.Devices;

/// <summary>
/// SteelSeries Arctis 7+ through its USB dongle (1038:220E). The dongle's vendor collection (page FFC0, usage 01,
/// interface 3) answers a status request {00 B0} with the headset's battery, charging and ChatMix
/// (the HeadsetControl protocol). That request is the only thing sent: it asks, it changes nothing.
/// </summary>
internal static class ArctisHeadset
{
    public const ushort Vendor = 0x1038, Arctis7Plus = 0x220E;
    public const string Model = "SteelSeries Arctis 7+";
    private const byte StatusCommand = 0xB0;

    public static async Task<DeviceReading> ReadAsync(IReadOnlyList<HidInfo> hid)
    {
        var d = hid.FirstOrDefault(x => x.Vid == Vendor && x.Pid == Arctis7Plus && x.UsagePage == 0xFFC0 && x.Usage == 0x01);
        if (d is null) return new() { Id = "headset", Model = Model, State = DeviceState.Missing };
        await using var fs = Hid.OpenStream(d.Path, write: true);
        if (fs is null) return new() { Id = "headset", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = "cannot open the dongle" };

        var cmd = new byte[Math.Max(2, d.OutputLength)];
        cmd[1] = StatusCommand; // cmd[0] is the report id (none)
        try { await fs.WriteAsync(cmd); }
        catch (IOException ex) { return new() { Id = "headset", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = ex.Message }; }

        // The dongle can also send its own reports (volume, ChatMix moves); wait for the status answer.
        var until = Environment.TickCount64 + 1000;
        while (Environment.TickCount64 < until)
        {
            var res = await Hid.ReadAsync(fs, d.InputLength, (int)Math.Max(1, until - Environment.TickCount64));
            if (res is null) break;
            if (Parse(res) is not { } s) continue;
            return s.Online
                ? new() { Id = "headset", Model = Model, State = DeviceState.Ready, Link = "dongle", Battery = s.Battery, Charging = s.Charging, ChatGame = s.Game, ChatChat = s.Chat }
                : new() { Id = "headset", Model = Model, State = DeviceState.Asleep, Link = "dongle" };
        }
        return new() { Id = "headset", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = "no status answer within 1 s" };
    }

    internal sealed record Status(bool Online, int? Battery, bool Charging, int? Game, int? Chat);

    /// <summary>
    /// The answer as ReadFile returns it: report id 0, the B0 echo, then HeadsetControl's data[1..]
    /// (HeadsetControl's data[i] is res[i + 1] here): connection (1 = headset off), battery 0-4,
    /// charging (1), ChatMix game 0-100, ChatMix chat 0-100.
    /// </summary>
    internal static Status? Parse(byte[] res)
    {
        if (res.Length < 7 || res[0] != 0x00 || res[1] != StatusCommand) return null;
        if (res[2] == 0x01) return new Status(false, null, false, null, null);
        var level = res[3];
        int? Pct(byte b) => b <= 100 ? b : null;
        return new Status(true, level > 4 ? 100 : level * 25, res[4] == 0x01, Pct(res[5]), Pct(res[6]));
    }
}
