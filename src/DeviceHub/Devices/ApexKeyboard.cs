using DeviceHub.Interop;
using DeviceHub.Lighting;
using Microsoft.Win32.SafeHandles;

namespace DeviceHub.Devices;

/// <summary>
/// SteelSeries Apex 5 (1038:161C). Its vendor collection on interface 1 (page FFC0, usage 01) takes:
/// <list type="bullet">
/// <item>feature report 0x3A, a colour for every key (OpenRGB's "Gen 1" Apex direct mode);</item>
/// <item>output report 0x3B, back to the keyboard's own saved lighting;</item>
/// <item>feature report 0x61, a 128 × 40 one-bit picture for the OLED screen (apex-tux's protocol).</item>
/// </list>
/// (The brightness commands 0x23 / 0xA3 belong to newer models: this Apex 5 never answers 0xA3, so brightness
/// scales the colours instead.) Colours and the picture last until the keyboard loses power.
/// </summary>
internal static class ApexKeyboard
{
    public const ushort Vendor = 0x1038, Apex5 = 0x161C;
    public const string Model = "SteelSeries Apex 5";
    private const int ReportLength = 643;

    /// <summary>Every key id the Apex family knows (HID usage codes), OpenRGB's list. Keys a model lacks are ignored.</summary>
    internal static readonly byte[] Keys =
    [
        0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
        0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B,
        0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40,
        0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0x51, 0x52, 0x64, 0xE0,
        0xE1, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xF0, 0x31, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
        0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, 0x63, 0xFB,
    ];

    /// <summary>The keyboard has no battery or sensor to read: this only says whether it is plugged in.</summary>
    public static Task<DeviceReading> ReadAsync(IReadOnlyList<HidInfo> hid) => Task.FromResult<DeviceReading>(Find(hid) is null
        ? new() { Id = "keyboard", Model = Model, State = DeviceState.Missing }
        : new() { Id = "keyboard", Model = Model, State = DeviceState.Ready, Link = "usb" });

    public static HidInfo? Find(IReadOnlyList<HidInfo> hid) =>
        hid.FirstOrDefault(x => x.Vid == Vendor && x.Pid == Apex5 && x.UsagePage == 0xFFC0 && x.Usage == 0x01 && x.FeatureLength >= ReportLength);

    /// <summary>A handle for feature reports (colours, the screen); keep it open while animating.</summary>
    public static SafeFileHandle? Open(HidInfo d) => Hid.Open(d.Path, readWrite: true);

    /// <summary>One colour per key. Keys missing from the map get the first colour given (or off).</summary>
    public static void SendColors(SafeFileHandle h, IReadOnlyDictionary<byte, Rgb> colors)
    {
        var report = DirectReport(colors);
        if (!Hid.HidD_SetFeature(h, report, report.Length)) throw new IOException("the keyboard refused the colours");
    }

    /// <summary>A picture on the OLED screen: 640 bytes, 128 × 40 pixels, row by row, leftmost pixel in the high bit.</summary>
    public static void SendScreen(SafeFileHandle h, ReadOnlySpan<byte> bits)
    {
        var report = ScreenReport(bits);
        if (!Hid.HidD_SetFeature(h, report, report.Length)) throw new IOException("the keyboard refused the picture");
    }

    /// <summary>Back to the lighting saved in the keyboard (its own profile).</summary>
    public static void UseOwnLighting(HidInfo d)
    {
        using var fs = Hid.OpenStream(d.Path, write: true) ?? throw new IOException("cannot open the keyboard");
        var buf = new byte[Math.Max(2, d.OutputLength)];
        buf[1] = 0x3B;
        fs.Write(buf);
    }

    internal static byte[] DirectReport(IReadOnlyDictionary<byte, Rgb> colors)
    {
        var buf = new byte[ReportLength];
        buf[1] = 0x3A;
        buf[2] = (byte)Keys.Length;
        var fallback = colors.Count > 0 ? colors.Values.First() : default;
        for (var i = 0; i < Keys.Length; i++)
        {
            var c = colors.TryGetValue(Keys[i], out var v) ? v : fallback;
            buf[3 + i * 4] = Keys[i];
            buf[4 + i * 4] = c.R;
            buf[5 + i * 4] = c.G;
            buf[6 + i * 4] = c.B;
        }
        return buf;
    }

    internal static byte[] ScreenReport(ReadOnlySpan<byte> bits)
    {
        if (bits.Length != 640) throw new ArgumentException("the screen takes 640 bytes", nameof(bits));
        var buf = new byte[ReportLength];
        buf[1] = 0x61;
        bits.CopyTo(buf.AsSpan(2));
        return buf;
    }
}
