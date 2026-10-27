namespace DeviceHub.Lighting;

/// <summary>
/// Where every key of a full-size ANSI keyboard sits, in key units (1 = one letter key), with its HID usage code:
/// the code the Apex takes a colour for. Effects use the positions (a wave moves left to right); the page draws
/// its preview from the same table, so what you see is what the keyboard gets.
/// </summary>
internal static class KeyboardLayout
{
    public sealed record Key(byte Code, double X, double Y, double W, double H);

    public const double Width = 22.5, Height = 6.5;

    public static readonly IReadOnlyList<Key> Keys = Build();

    /// <summary>W A S D and the arrows: the keys an accent colour picks out.</summary>
    public static readonly HashSet<byte> Accent = [0x1A, 0x04, 0x16, 0x07, 0x4F, 0x50, 0x51, 0x52];

    private static List<Key> Build()
    {
        var keys = new List<Key>();
        // Each row: (code, width); code 0 is a gap. Tall keys (numpad + and Enter) are marked with a negative width.
        void Row(double y, double x, params (byte Code, double W)[] row)
        {
            foreach (var (code, w) in row)
            {
                var width = Math.Abs(w);
                if (code != 0) keys.Add(new Key(code, x, y, width, w < 0 ? 2 : 1));
                x += width;
            }
        }

        // Function row
        Row(0, 0, (0x29, 1), (0, 1), (0x3A, 1), (0x3B, 1), (0x3C, 1), (0x3D, 1), (0, 0.5), (0x3E, 1), (0x3F, 1), (0x40, 1), (0x41, 1), (0, 0.5), (0x42, 1), (0x43, 1), (0x44, 1), (0x45, 1));
        Row(0, 15.25, (0x46, 1), (0x47, 1), (0x48, 1));
        // Number row
        Row(1.5, 0, (0x35, 1), (0x1E, 1), (0x1F, 1), (0x20, 1), (0x21, 1), (0x22, 1), (0x23, 1), (0x24, 1), (0x25, 1), (0x26, 1), (0x27, 1), (0x2D, 1), (0x2E, 1), (0x2A, 2));
        Row(1.5, 15.25, (0x49, 1), (0x4A, 1), (0x4B, 1));
        Row(1.5, 18.5, (0x53, 1), (0x54, 1), (0x55, 1), (0x56, 1));
        // Q row
        Row(2.5, 0, (0x2B, 1.5), (0x14, 1), (0x1A, 1), (0x08, 1), (0x15, 1), (0x17, 1), (0x1C, 1), (0x18, 1), (0x0C, 1), (0x12, 1), (0x13, 1), (0x2F, 1), (0x30, 1), (0x31, 1.5));
        Row(2.5, 15.25, (0x4C, 1), (0x4D, 1), (0x4E, 1));
        Row(2.5, 18.5, (0x5F, 1), (0x60, 1), (0x61, 1), (0x57, -1));
        // A row
        Row(3.5, 0, (0x39, 1.75), (0x04, 1), (0x16, 1), (0x07, 1), (0x09, 1), (0x0A, 1), (0x0B, 1), (0x0D, 1), (0x0E, 1), (0x0F, 1), (0x33, 1), (0x34, 1), (0x28, 2.25));
        Row(3.5, 18.5, (0x5C, 1), (0x5D, 1), (0x5E, 1));
        // Z row
        Row(4.5, 0, (0xE1, 2.25), (0x1D, 1), (0x1B, 1), (0x06, 1), (0x19, 1), (0x05, 1), (0x11, 1), (0x10, 1), (0x36, 1), (0x37, 1), (0x38, 1), (0xE5, 2.75));
        Row(4.5, 16.25, (0x52, 1));
        Row(4.5, 18.5, (0x59, 1), (0x5A, 1), (0x5B, 1), (0x58, -1));
        // Bottom row: the Apex has the SteelSeries key (0xF0) where others have Fn
        Row(5.5, 0, (0xE0, 1.25), (0xE3, 1.25), (0xE2, 1.25), (0x2C, 6.25), (0xE6, 1.25), (0xF0, 1.25), (0x65, 1.25), (0xE4, 1.25));
        Row(5.5, 15.25, (0x50, 1), (0x51, 1), (0x4F, 1));
        Row(5.5, 18.5, (0x62, 2), (0x63, 1));
        return keys;
    }
}
