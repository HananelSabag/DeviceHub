using System.Runtime.InteropServices;
using DeviceHub.Interop;
using Microsoft.Win32.SafeHandles;

namespace DeviceHub.Devices;

/// <summary>
/// Razer Basilisk X HyperSpeed through its USB receiver (1532:0083). Razer devices answer 90-byte feature reports
/// on interface 0 (the OpenRazer protocol). Only "get" commands are sent: battery, polling rate and DPI.
/// </summary>
internal static class RazerMouse
{
    public const ushort Vendor = 0x1532, BasiliskXHyperSpeed = 0x0083;
    public const string Model = "Razer Basilisk X HyperSpeed";

    // The receiver answers transaction id 0x1F (confirmed on this mouse); 0xFF is the id older Razer devices use.
    private static readonly byte[] TransactionIds = [0x1F, 0xFF];
    private static byte? knownTid;

    public static DeviceReading Read(IReadOnlyList<HidInfo> hid)
    {
        var d = hid.FirstOrDefault(x => x.Vid == Vendor && x.Pid == BasiliskXHyperSpeed && x.FeatureLength == 91);
        if (d is null) return new() { Id = "mouse", Model = Model, State = DeviceState.Missing };
        using var h = Hid.Open(d.Path, readWrite: true) ?? Hid.Open(d.Path, readWrite: false);
        if (h is null) return new() { Id = "mouse", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = $"cannot open the receiver ({Marshal.GetLastWin32Error()})" };

        var asleep = false;
        string? failure = null;
        foreach (var tid in knownTid is { } k ? [k] : TransactionIds)
        {
            var (status, battery) = Query(h, tid, 0x07, 0x80, 0x02);
            if (status == Status.Timeout) { asleep = true; break; } // the receiver is there, the mouse didn't answer
            if (status != Status.Ok || battery is null) { failure = $"battery query: {status}"; continue; }
            knownTid = tid;
            var (_, poll) = Query(h, tid, 0x00, 0x85, 0x01);
            var (_, dpi) = Query(h, tid, 0x04, 0x85, 0x07, 0x01); // 0x01: the mouse's current (volatile) DPI
            return new()
            {
                Id = "mouse", Model = Model, State = DeviceState.Ready, Link = "dongle",
                Battery = BatteryPercent(battery),
                PollingHz = poll is null ? null : PollingHz(poll),
                Dpi = dpi is null ? null : (dpi[9] << 8) | dpi[10],
                DpiY = dpi is null ? null : (dpi[11] << 8) | dpi[12],
            };
        }
        return asleep
            ? new() { Id = "mouse", Model = Model, State = DeviceState.Asleep, Link = "dongle" }
            : new() { Id = "mouse", Model = Model, State = DeviceState.Error, Link = "dongle", Detail = failure ?? "no answer" };
    }

    internal enum Status { Ok, Busy, Failure, Timeout, NotSupported, Unknown, IoError }

    /// <summary>Sends one "get" command and returns the 90-byte answer (report id stripped).</summary>
    private static (Status, byte[]?) Query(SafeFileHandle h, byte tid, byte cls, byte id, byte size, byte arg0 = 0)
    {
        var buf = BuildQuery(tid, cls, id, size, arg0);
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

    /// <summary>Report id 0, then the packet: status, transaction id, 2 × remaining, protocol, size, class, command, arguments, CRC.</summary>
    internal static byte[] BuildQuery(byte tid, byte cls, byte id, byte size, byte arg0 = 0)
    {
        var buf = new byte[91];
        buf[2] = tid; buf[6] = size; buf[7] = cls; buf[8] = id; buf[9] = arg0;
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
}
