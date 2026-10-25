using System.Runtime.InteropServices;
using DeviceHub.Interop;
using Microsoft.Win32.SafeHandles;

namespace DeviceHub.Devices;

/// <summary>
/// Razer Basilisk X HyperSpeed through its USB receiver (1532:0083). Razer devices answer 90-byte feature reports
/// on interface 0 (the OpenRazer protocol): battery, polling rate, DPI and DPI stages, sleep time and the
/// low-battery warning. Every setting written is read back to confirm the mouse took it.
/// </summary>
internal static class RazerMouse
{
    public const ushort Vendor = 0x1532, BasiliskXHyperSpeed = 0x0083;
    public const string Model = "Razer Basilisk X HyperSpeed";
    private const byte VarStore = 0x01; // the mouse's saved settings, as opposed to 0x00 "don't store"

    // The receiver answers transaction id 0x1F (confirmed on this mouse); 0xFF is the id OpenRazer uses for it.
    private static readonly byte[] TransactionIds = [0x1F, 0xFF];
    private static byte? knownTid;

    public static DeviceReading Read(IReadOnlyList<HidInfo> hid)
    {
        using var s = Session.Open(hid, out var missing);
        if (s is null) return missing!;
        var (status, battery) = s.Get(0x07, 0x80, 0x02);
        if (status == Status.Timeout) return new() { Id = "mouse", Model = Model, State = DeviceState.Asleep, Link = "dongle" };
        if (battery is null) return new() { Id = "mouse", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = $"battery query: {status}" };
        var (_, poll) = s.Get(0x00, 0x85, 0x01);
        var (_, dpi) = s.Get(0x04, 0x85, 0x07, VarStore);
        return new()
        {
            Id = "mouse", Model = Model, State = DeviceState.Ready, Link = "dongle",
            Battery = BatteryPercent(battery),
            PollingHz = poll is null ? null : PollingHz(poll),
            Dpi = dpi is null ? null : (dpi[9] << 8) | dpi[10],
            DpiY = dpi is null ? null : (dpi[11] << 8) | dpi[12],
        };
    }

    /// <summary>Everything the mouse page shows. Asked only when the page opens: each answer is a wireless round trip.</summary>
    public sealed record Settings(int? PollingHz, int? Dpi, List<int> Stages, int ActiveStage, int? SleepSeconds, int? LowBatteryPercent);

    public static Settings? ReadSettings(IReadOnlyList<HidInfo> hid)
    {
        using var s = Session.Open(hid, out _);
        if (s is null) return null;
        var (status, poll) = s.Get(0x00, 0x85, 0x01);
        if (status == Status.Timeout) return null; // asleep
        var (_, dpi) = s.Get(0x04, 0x85, 0x07, VarStore);
        var (_, stages) = s.Get(0x04, 0x86, 0x26, VarStore);
        var (_, idle) = s.Get(0x07, 0x83, 0x02);
        var (_, low) = s.Get(0x07, 0x81, 0x01);
        var parsed = stages is null ? null : ParseStages(stages);
        return new Settings(
            poll is null ? null : PollingHz(poll),
            dpi is null ? null : (dpi[9] << 8) | dpi[10],
            parsed?.Dpi ?? [],
            parsed?.Active ?? 0,
            idle is null ? null : (idle[8] << 8) | idle[9],
            low is null ? null : (int)Math.Round(low[8] * 100 / 255.0));
    }

    public static string SetPolling(IReadOnlyList<HidInfo> hid, int hz)
    {
        byte code = hz switch { 1000 => 0x01, 500 => 0x02, 125 => 0x08, _ => throw new ArgumentOutOfRangeException(nameof(hz)) };
        using var s = Session.Require(hid);
        s.Set(0x00, 0x05, 0x01, [code]);
        var (_, back) = s.Get(0x00, 0x85, 0x01);
        return back is not null && PollingHz(back) == hz ? "ok" : throw new IOException("the mouse kept its old polling rate");
    }

    /// <summary>The current DPI (same on both axes). The DPI button still steps through the stages.</summary>
    public static string SetDpi(IReadOnlyList<HidInfo> hid, int dpi)
    {
        dpi = Math.Clamp(dpi, 100, 16000);
        using var s = Session.Require(hid);
        s.Set(0x04, 0x05, 0x07, [VarStore, (byte)(dpi >> 8), (byte)dpi, (byte)(dpi >> 8), (byte)dpi, 0, 0]);
        var (_, back) = s.Get(0x04, 0x85, 0x07, VarStore);
        return back is not null && ((back[9] << 8) | back[10]) == dpi ? "ok" : throw new IOException("the mouse kept its old DPI");
    }

    /// <summary>
    /// The DPI button's stages (1-5) and which one is active; the active stage's DPI becomes the current DPI.
    /// Stage numbers are written the way the mouse reported them, so its own numbering is kept.
    /// </summary>
    public static string SetStages(IReadOnlyList<HidInfo> hid, IReadOnlyList<int> stages, int active)
    {
        if (stages.Count is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(stages), "1 to 5 stages");
        var dpi = stages.Select(d => Math.Clamp(d, 100, 16000)).ToList();
        active = Math.Clamp(active, 0, dpi.Count - 1);
        using var s = Session.Require(hid);
        var (_, current) = s.Get(0x04, 0x86, 0x26, VarStore);
        var firstNumber = current is not null && ParseStages(current) is { } c ? c.FirstNumber : (byte)1;
        s.Set(0x04, 0x06, 0x26, BuildStages(dpi, active, firstNumber));
        s.Set(0x04, 0x05, 0x07, [VarStore, (byte)(dpi[active] >> 8), (byte)dpi[active], (byte)(dpi[active] >> 8), (byte)dpi[active], 0, 0]);
        var (_, back) = s.Get(0x04, 0x86, 0x26, VarStore);
        return back is not null && ParseStages(back) is { } b && b.Dpi.SequenceEqual(dpi) && b.Active == active
            ? "ok" : throw new IOException("the mouse kept its old DPI stages");
    }

    /// <summary>How long the mouse waits before it sleeps: 60-900 seconds.</summary>
    public static string SetSleep(IReadOnlyList<HidInfo> hid, int seconds)
    {
        seconds = Math.Clamp(seconds, 60, 900);
        using var s = Session.Require(hid);
        s.Set(0x07, 0x03, 0x02, [(byte)(seconds >> 8), (byte)seconds]);
        var (_, back) = s.Get(0x07, 0x83, 0x02);
        return back is not null && ((back[8] << 8) | back[9]) == seconds ? "ok" : throw new IOException("the mouse kept its old sleep time");
    }

    /// <summary>The battery level at which the mouse starts warning: 5-25 %.</summary>
    public static string SetLowBattery(IReadOnlyList<HidInfo> hid, int percent)
    {
        var raw = (byte)Math.Clamp((int)Math.Round(percent * 255 / 100.0), 0x0C, 0x3F);
        using var s = Session.Require(hid);
        s.Set(0x07, 0x01, 0x01, [raw]);
        var (_, back) = s.Get(0x07, 0x81, 0x01);
        return back is not null && back[8] == raw ? "ok" : throw new IOException("the mouse kept its old warning level");
    }

    internal enum Status { Ok, Busy, Failure, Timeout, NotSupported, Unknown, IoError }

    /// <summary>One open handle to the receiver and the transaction id it answers to.</summary>
    private sealed class Session : IDisposable
    {
        private readonly SafeFileHandle h;
        private readonly byte tid;
        private Session(SafeFileHandle h, byte tid) { this.h = h; this.tid = tid; }

        public static Session? Open(IReadOnlyList<HidInfo> hid, out DeviceReading? missing)
        {
            missing = null;
            var d = hid.FirstOrDefault(x => x.Vid == Vendor && x.Pid == BasiliskXHyperSpeed && x.FeatureLength == 91);
            if (d is null) { missing = new() { Id = "mouse", Model = Model, State = DeviceState.Missing }; return null; }
            var h = Hid.Open(d.Path, readWrite: true) ?? Hid.Open(d.Path, readWrite: false);
            if (h is null) { missing = new() { Id = "mouse", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = $"cannot open the receiver ({Marshal.GetLastWin32Error()})" }; return null; }
            if (knownTid is { } known) return new Session(h, known);
            // Find the transaction id with a harmless question; keep the first that is answered.
            foreach (var tid in TransactionIds)
            {
                var (status, _) = Query(h, tid, 0x07, 0x80, 0x02, []);
                if (status == Status.Ok) { knownTid = tid; return new Session(h, tid); }
                if (status == Status.Timeout) return new Session(h, tid); // the receiver is there, the mouse sleeps
            }
            return new Session(h, TransactionIds[0]);
        }

        public static Session Require(IReadOnlyList<HidInfo> hid) =>
            Open(hid, out var missing) ?? throw new IOException(missing?.State == DeviceState.Missing ? "the Razer receiver isn't plugged in" : missing?.Detail ?? "cannot open the receiver");

        public (Status, byte[]?) Get(byte cls, byte id, byte size, byte arg0 = 0) => Query(h, tid, cls, id, size, [arg0]);

        public void Set(byte cls, byte id, byte size, byte[] args)
        {
            var (status, _) = Query(h, tid, cls, id, size, args);
            if (status == Status.Timeout) throw new IOException("the mouse is asleep: move it and try again");
            if (status != Status.Ok) throw new IOException($"the mouse refused the change ({status})");
        }

        public void Dispose() => h.Dispose();
    }

    /// <summary>Sends one command and returns the 90-byte answer (report id stripped).</summary>
    private static (Status, byte[]?) Query(SafeFileHandle h, byte tid, byte cls, byte id, byte size, byte[] args)
    {
        var buf = BuildPacket(tid, cls, id, size, args);
        if (!Hid.HidD_SetFeature(h, buf, buf.Length)) return (Status.IoError, null);
        for (var attempt = 0; attempt < 8; attempt++)
        {
            Thread.Sleep(attempt == 0 ? 40 : 60);
            var res = new byte[91];
            if (!Hid.HidD_GetFeature(h, res, res.Length)) return (Status.IoError, null);
            var status = ParseStatus(res[1]);
            if (status == Status.Busy) continue;
            // The answer must be for this command; a stale one from another program would carry another class/id.
            if (status == Status.Ok && (res[7] != cls || res[8] != id)) continue;
            return (status, status == Status.Ok ? res[1..] : null);
        }
        return (Status.Busy, null);
    }

    internal static byte[] BuildQuery(byte tid, byte cls, byte id, byte size, byte arg0 = 0) => BuildPacket(tid, cls, id, size, [arg0]);

    /// <summary>Report id 0, then the packet: status, transaction id, 2 × remaining, protocol, size, class, command, arguments, CRC.</summary>
    internal static byte[] BuildPacket(byte tid, byte cls, byte id, byte size, ReadOnlySpan<byte> args)
    {
        if (args.Length > 80) throw new ArgumentOutOfRangeException(nameof(args));
        var buf = new byte[91];
        buf[2] = tid; buf[6] = size; buf[7] = cls; buf[8] = id;
        args.CopyTo(buf.AsSpan(9));
        buf[89] = Crc(buf);
        return buf;
    }

    /// <summary>XOR of packet bytes 2..87 (buffer bytes 3..88, after the report id).</summary>
    internal static byte Crc(byte[] buf)
    {
        byte crc = 0;
        for (var i = 3; i <= 88; i++) crc ^= buf[i];
        return crc;
    }

    internal static Status ParseStatus(byte b) => b switch
    {
        0x02 => Status.Ok,
        0x01 => Status.Busy,
        0x03 => Status.Failure,
        0x04 => Status.Timeout,
        0x05 => Status.NotSupported,
        _ => Status.Unknown,
    };

    /// <summary>The packet (report id stripped): battery is the second argument, 0-255.</summary>
    internal static int BatteryPercent(byte[] packet) => (int)Math.Round(packet[9] * 100 / 255.0);

    internal static int? PollingHz(byte[] packet) => packet[8] switch
    {
        0x01 => 1000,
        0x02 => 500,
        0x08 => 125,
        _ => null,
    };

    internal sealed record StageList(List<int> Dpi, int Active, byte FirstNumber);

    /// <summary>
    /// Arguments (packet byte 8 on): store, active stage number, count, then 7 bytes a stage:
    /// number, DPI X (2), DPI Y (2), 2 reserved. Active is returned as an index into the list.
    /// </summary>
    internal static StageList? ParseStages(byte[] packet)
    {
        var a = packet.AsSpan(8);
        int count = a[2];
        if (count is < 1 or > 5) return null;
        var dpi = new List<int>();
        var numbers = new List<byte>();
        for (var i = 0; i < count; i++)
        {
            var e = 3 + i * 7;
            numbers.Add(a[e]);
            dpi.Add((a[e + 1] << 8) | a[e + 2]);
        }
        var active = numbers.IndexOf(a[1]);
        return new StageList(dpi, Math.Max(0, active), numbers[0]);
    }

    internal static byte[] BuildStages(IReadOnlyList<int> dpi, int active, byte firstNumber)
    {
        var args = new byte[3 + dpi.Count * 7];
        args[0] = VarStore;
        args[1] = (byte)(firstNumber + active);
        args[2] = (byte)dpi.Count;
        for (var i = 0; i < dpi.Count; i++)
        {
            var e = 3 + i * 7;
            args[e] = (byte)(firstNumber + i);
            args[e + 1] = (byte)(dpi[i] >> 8); args[e + 2] = (byte)dpi[i];
            args[e + 3] = (byte)(dpi[i] >> 8); args[e + 4] = (byte)dpi[i];
        }
        return args;
    }
}
