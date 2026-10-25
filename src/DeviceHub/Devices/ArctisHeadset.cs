using DeviceHub.Interop;

namespace DeviceHub.Devices;

/// <summary>
/// SteelSeries Arctis 7+ through its USB dongle (1038:220E). The dongle's vendor collection (page FFC0, usage 01,
/// interface 3) speaks the HeadsetControl protocol: a status request {00 B0} answers battery, charging and ChatMix;
/// sidetone, auto-off and the equalizer are set with one command each, followed by {00 09} to save it in the
/// headset. The headset cannot report those three back, so DeviceHub remembers what it last set.
/// </summary>
internal static class ArctisHeadset
{
    public const ushort Vendor = 0x1038, Arctis7Plus = 0x220E;
    public const string Model = "SteelSeries Arctis 7+";
    private const byte StatusCommand = 0xB0;

    /// <summary>The presets SteelSeries ships, as dB per band (31 Hz … 16 kHz). HeadsetControl's values.</summary>
    public static readonly Dictionary<string, double[]> Presets = new()
    {
        ["flat"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        ["bass"] = [3.5, 4.0, 1.0, -1.5, -1.5, -1.0, -1.0, -1.0, -1.0, 5.5],
        ["smiley"] = [3.0, 1.5, -1.5, -4.0, -4.0, -2.5, 1.5, 3.0, 4.0, 3.5],
        ["focus"] = [-5.0, -1.0, -3.5, -2.5, 4.0, 6.0, 3.5, -3.5, 0.0, -3.5],
    };

    public static async Task<DeviceReading> ReadAsync(IReadOnlyList<HidInfo> hid)
    {
        var d = Find(hid);
        if (d is null) return new() { Id = "headset", Model = Model, State = DeviceState.Missing };
        await using var fs = Hid.OpenStream(d.Path, write: true);
        if (fs is null) return new() { Id = "headset", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = "cannot open the dongle" };
        try { await fs.WriteAsync(Command(d, 0xB0)); }
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

    /// <summary>Sidetone: how much of your own voice you hear. 0 off … 3 high.</summary>
    public static Task SetSidetone(IReadOnlyList<HidInfo> hid, int level) => SendAndSave(hid, 0x39, (byte)Math.Clamp(level, 0, 3));

    /// <summary>Switch off after this many idle minutes; 0 never.</summary>
    public static Task SetAutoOff(IReadOnlyList<HidInfo> hid, int minutes) => SendAndSave(hid, 0xA3, (byte)Math.Clamp(minutes, 0, 90));

    /// <summary>Ten bands, -12 … +12 dB in half steps.</summary>
    public static Task SetEqualizer(IReadOnlyList<HidInfo> hid, IReadOnlyList<double> bands) => SendAndSave(hid, 0x33, EqualizerBytes(bands));

    internal static byte[] EqualizerBytes(IReadOnlyList<double> bands)
    {
        if (bands.Count != 10) throw new ArgumentException("the equalizer has 10 bands", nameof(bands));
        var b = new byte[11]; // the bands, then a 0
        for (var i = 0; i < 10; i++) b[i] = (byte)(0x18 + (int)Math.Round(Math.Clamp(bands[i], -12, 12) * 2));
        return b;
    }

    private static async Task SendAndSave(IReadOnlyList<HidInfo> hid, byte command, params byte[] args)
    {
        var d = Find(hid) ?? throw new IOException("the headset's dongle isn't plugged in");
        // Settings travel through the dongle to the headset; with the headset off they would be lost.
        if (await ReadAsync(hid) is { State: not DeviceState.Ready }) throw new IOException("the headset is off: switch it on and try again");
        await using var fs = Hid.OpenStream(d.Path, write: true) ?? throw new IOException("cannot open the dongle");
        await fs.WriteAsync(Command(d, command, args));
        await Task.Delay(60);
        await fs.WriteAsync(Command(d, 0x09)); // save, so the headset keeps it after a power cycle
        await Task.Delay(60);
    }

    /// <summary>An output report: report id 0, the command, its arguments, zero padding to the report's length.</summary>
    internal static byte[] Command(HidInfo d, byte command, params byte[] args)
    {
        var buf = new byte[Math.Max(2 + args.Length, d.OutputLength)];
        buf[1] = command;
        args.CopyTo(buf, 2);
        return buf;
    }

    private static HidInfo? Find(IReadOnlyList<HidInfo> hid) =>
        hid.FirstOrDefault(x => x.Vid == Vendor && x.Pid == Arctis7Plus && x.UsagePage == 0xFFC0 && x.Usage == 0x01);

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
