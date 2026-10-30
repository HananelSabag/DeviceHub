using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using DeviceHub.Devices;

namespace DeviceHub.Lighting;

/// <summary>What the keyboard's OLED shows. off: DeviceHub leaves it alone.</summary>
internal sealed record OledSettings
{
    /// <summary>off | clock | devices | system | music | text | image | cycle | show | stars | matrix | energy | sound | goal | power</summary>
    public string Mode { get; init; } = "off";
    public string Text { get; init; } = "";
}

/// <summary>
/// Draws the Apex's 128 × 40 one-bit screen with GDI+ (text drawn without smoothing, pictures dithered) and packs it
/// the way the keyboard takes it: 640 bytes, row by row, leftmost pixel in the high bit.
/// </summary>
internal static class OledScreen
{
    public const int W = 128, H = 40;

    private static readonly string Digits = InstalledOr("Bahnschrift", "Segoe UI");
    private const string Body = "Segoe UI";

    private static string InstalledOr(string name, string fallback)
    {
        try { using var test = new FontFamily(name); return name; }
        catch (ArgumentException) { return fallback; }
    }

    internal static (Bitmap, Graphics) Canvas()
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        var g = Graphics.FromImage(bmp);
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        return (bmp, g);
    }

    /// <summary>White pixels on, everything else off; shifted by (dx, dy) so a still picture never burns in.</summary>
    public static byte[] Pack(Bitmap bmp, int dx = 0, int dy = 0)
    {
        var bits = new byte[W * H / 8];
        var data = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var px = new int[W * H];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
            for (var y = 0; y < H; y++)
                for (var x = 0; x < W; x++)
                {
                    int sx = x - dx, sy = y - dy;
                    if (sx < 0 || sy < 0 || sx >= W || sy >= H) continue;
                    var c = px[sy * W + sx];
                    if (((c >> 16) & 0xFF) + ((c >> 8) & 0xFF) + (c & 0xFF) > 3 * 127) bits[y * 16 + x / 8] |= (byte)(0x80 >> (x % 8));
                }
        }
        finally { bmp.UnlockBits(data); }
        return bits;
    }

    /// <summary>A pixel shift that walks a small square once every two minutes.</summary>
    public static (int Dx, int Dy) Shift(DateTime now) => ((now.Minute / 2) % 4) switch { 0 => (0, 0), 1 => (1, 0), 2 => (1, 1), _ => (0, 1) };

    private static bool Hebrew(string s) => s.Any(c => c is >= '֐' and <= '׿');

    private static StringFormat Format(string text, StringAlignment align = StringAlignment.Near)
    {
        var f = new StringFormat(StringFormat.GenericTypographic) { Alignment = align, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.None };
        f.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
        if (Hebrew(text)) f.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        return f;
    }

    /// <summary>The time large, the day and date beside it, the seconds as a line along the bottom.</summary>
    public static byte[] Clock(DateTime now, bool english)
    {
        var (bmp, g) = Canvas();
        using (bmp) using (g)
        {
            using var big = new Font(Digits, 31, FontStyle.Bold, GraphicsUnit.Pixel);
            using var small = new Font(Body, 11, FontStyle.Bold, GraphicsUnit.Pixel);
            var culture = CultureInfo.GetCultureInfo(english ? "en-GB" : "he-IL");
            var time = now.ToString("HH:mm", culture);
            using var fmt = Format(time);
            g.DrawString(time, big, Brushes.White, -1, 3, fmt);
            var day = english ? now.ToString("ddd", culture) : culture.DateTimeFormat.GetAbbreviatedDayName(now.DayOfWeek);
            var date = now.ToString("dd.MM", culture);
            RightAligned(g, day, small, 6);
            RightAligned(g, date, small, 20);
            g.FillRectangle(Brushes.White, 0, 38, (int)Math.Round(W * now.Second / 59.0), 2);
            return Pack(bmp, 0, 0);
        }
    }

    /// <summary>A clock face on the left (hour, minute and second hands), the day and the date beside it.</summary>
    public static byte[] Analog(DateTime now, bool english)
    {
        var (bmp, g) = Canvas();
        using (bmp) using (g)
        {
            const float cx = 20, cy = 20, r = 18;
            g.DrawEllipse(Pens.White, cx - r, cy - r, r * 2, r * 2);
            for (var i = 0; i < 12; i++)
            {
                var a = i * Math.PI / 6;
                var inner = i % 3 == 0 ? r - 4 : r - 2;
                g.DrawLine(Pens.White, cx + (float)(Math.Sin(a) * inner), cy - (float)(Math.Cos(a) * inner), cx + (float)(Math.Sin(a) * (r - 1)), cy - (float)(Math.Cos(a) * (r - 1)));
            }
            void Hand(double turns, float length, int width)
            {
                using var pen = new Pen(Color.White, width);
                var a = turns * Math.Tau;
                g.DrawLine(pen, cx, cy, cx + (float)(Math.Sin(a) * length), cy - (float)(Math.Cos(a) * length));
            }
            Hand((now.Hour % 12 + now.Minute / 60.0) / 12, 9, 2);
            Hand((now.Minute + now.Second / 60.0) / 60, 14, 2);
            Hand(now.Second / 60.0, 15, 1);
            var culture = CultureInfo.GetCultureInfo(english ? "en-GB" : "he-IL");
            using var big = new Font(Digits, 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using var small = new Font(Body, 11, FontStyle.Bold, GraphicsUnit.Pixel);
            var day = english ? now.ToString("dddd", culture) : culture.DateTimeFormat.GetDayName(now.DayOfWeek);
            RightAligned(g, now.ToString("HH:mm", culture), big, 2);
            RightAligned(g, day, small, 21);
            return Pack(bmp);
        }
    }

    /// <summary>Text against the right edge, Hebrew or not (measured, so right-to-left text can't flip to the left).</summary>
    private static void RightAligned(Graphics g, string text, Font font, int y)
    {
        using var fmt = Format(text);
        var width = g.MeasureString(text, font, 1000, fmt).Width;
        g.DrawString(text, font, Brushes.White, new RectangleF(W - width - 1, y, width + 2, 16), fmt);
    }

    /// <summary>Four columns: mouse, headset and controller batteries, and the laptop's temperature.</summary>
    public static byte[] Devices(IReadOnlyList<DeviceReading> devices, (int Dx, int Dy) shift)
    {
        var (bmp, g) = Canvas();
        using (bmp) using (g)
        {
            using var num = new Font(Digits, 15, FontStyle.Bold, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.White, 1);
            string Value(string id)
            {
                var d = devices.FirstOrDefault(x => x.Id == id);
                if (d is null || d.State != DeviceState.Ready) return "--";
                return id == "laptop" ? (d.Temperature is { } t ? $"{t}°" : "--") : d.Battery is { } b ? $"{b}" : "--";
            }
            var cols = new[] { "mouse", "headset", "controller", "laptop" };
            for (var i = 0; i < cols.Length; i++)
            {
                var cx = i * 32 + 16;
                Icon(g, pen, cols[i], cx, 2);
                using var center = Format("0", StringAlignment.Center);
                g.DrawString(Value(cols[i]), num, Brushes.White, new RectangleF(cx - 16, 19, 32, 20), center);
                var d = devices.FirstOrDefault(x => x.Id == cols[i]);
                if (d is { Charging: true }) g.FillRectangle(Brushes.White, cx - 5, 37, 10, 2);
            }
            return Pack(bmp, shift.Dx, shift.Dy);
        }
    }

    /// <summary>The laptop at work: CPU and GPU, each with its temperature and a bar for how busy it is.</summary>
    public static byte[] Workload(DeviceReading? laptop, (int Dx, int Dy) shift)
    {
        var (bmp, g) = Canvas();
        using (bmp) using (g)
        {
            using var label = new Font(Body, 10, FontStyle.Bold, GraphicsUnit.Pixel);
            using var num = new Font(Digits, 15, FontStyle.Bold, GraphicsUnit.Pixel);
            void Line(int y, string name, int? temp, int? load)
            {
                g.DrawString(name, label, Brushes.White, 0, y + 3);
                g.DrawString(temp is { } t ? $"{t}°" : "--", num, Brushes.White, 26, y);
                g.DrawRectangle(Pens.White, 66, y + 5, 61, 8);
                if (load is { } l) g.FillRectangle(Brushes.White, 68, y + 7, (int)Math.Round(57 * Math.Clamp(l, 0, 100) / 100.0), 5);
            }
            Line(1, "CPU", laptop?.Temperature, laptop?.CpuLoad);
            Line(21, "GPU", laptop?.GpuTemperature, laptop?.GpuLoad);
            return Pack(bmp, shift.Dx, shift.Dy);
        }
    }

    /// <summary>Small line drawings, 14 px tall, centred on cx.</summary>
    private static void Icon(Graphics g, Pen p, string id, int cx, int y)
    {
        switch (id)
        {
            case "mouse":
                g.DrawRectangle(p, cx - 4, y, 8, 13);
                g.DrawLine(p, cx, y + 1, cx, y + 4);
                break;
            case "headset":
                g.DrawArc(p, cx - 7, y, 14, 14, 180, 180);
                g.FillRectangle(Brushes.White, cx - 7, y + 7, 3, 7);
                g.FillRectangle(Brushes.White, cx + 5, y + 7, 3, 7);
                break;
            case "controller":
                g.DrawRectangle(p, cx - 8, y + 3, 16, 8);
                g.DrawLine(p, cx - 8, y + 11, cx - 8, y + 13);
                g.DrawLine(p, cx + 8, y + 11, cx + 8, y + 13);
                g.FillRectangle(Brushes.White, cx - 5, y + 6, 3, 3);
                g.FillRectangle(Brushes.White, cx + 3, y + 6, 3, 3);
                break;
            default: // a thermometer
                g.DrawRectangle(p, cx - 1, y, 3, 9);
                g.DrawEllipse(p, cx - 3, y + 8, 7, 6);
                g.FillRectangle(Brushes.White, cx, y + 4, 2, 6);
                break;
        }
    }

    /// <summary>The song and the artist, scrolling when they don't fit, and how far into the song.</summary>
    public static byte[] Music(NowPlaying.Track track, double t)
    {
        var (bmp, g) = Canvas();
        using (bmp) using (g)
        {
            using var titleFont = new Font(Body, 14, FontStyle.Bold, GraphicsUnit.Pixel);
            using var artistFont = new Font(Body, 11, FontStyle.Regular, GraphicsUnit.Pixel);
            Marquee(g, track.Title, titleFont, 1, t);
            Marquee(g, track.Artist, artistFont, 19, t);
            if (track.Duration > TimeSpan.Zero)
            {
                var f = Math.Clamp(track.Position.TotalSeconds / track.Duration.TotalSeconds, 0, 1);
                g.DrawRectangle(Pens.White, 0, 35, W - 1, 4);
                g.FillRectangle(Brushes.White, 1, 36, (int)Math.Round((W - 2) * f), 3);
            }
            if (!track.Playing) { g.FillRectangle(Brushes.Black, W - 10, 33, 10, 7); g.FillRectangle(Brushes.White, W - 8, 34, 2, 6); g.FillRectangle(Brushes.White, W - 4, 34, 2, 6); }
            return Pack(bmp);
        }
    }

    /// <summary>One line of text: still when it fits, otherwise moving at 22 px a second with a gap between laps.</summary>
    private static void Marquee(Graphics g, string text, Font font, int y, double t)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var fmt = Format(text);
        var width = (int)Math.Ceiling(g.MeasureString(text, font, 4000, fmt).Width);
        var rtl = Hebrew(text);
        if (width <= W)
        {
            g.DrawString(text, font, Brushes.White, new RectangleF(0, y, W, 20), fmt);
            return;
        }
        var lap = width + 40;
        var offset = (int)(t * 22) % lap;
        for (var copy = 0; copy < 2; copy++)
        {
            var x = rtl ? W - width + offset - copy * lap : -offset + copy * lap;
            g.DrawString(text, font, Brushes.White, new RectangleF(x, y, width + 4, 20), fmt);
        }
    }

    /// <summary>Your own words, as large as they fit on one or two lines.</summary>
    public static byte[] Text(string text, (int Dx, int Dy) shift)
    {
        var (bmp, g) = Canvas();
        using (bmp) using (g)
        {
            text = string.IsNullOrWhiteSpace(text) ? "DeviceHub" : text.Trim();
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            if (Hebrew(text)) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            for (var size = 30; size >= 9; size--)
            {
                using var font = new Font(Body, size, FontStyle.Bold, GraphicsUnit.Pixel);
                var need = g.MeasureString(text, font, W, fmt);
                if (need.Width <= W && need.Height <= H && LineCount(g, text, font) <= 2)
                {
                    g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, W, H), fmt);
                    break;
                }
            }
            return Pack(bmp, shift.Dx, shift.Dy);
        }
    }

    private static int LineCount(Graphics g, string text, Font font)
    {
        g.MeasureString(text, font, new SizeF(W, 1000), StringFormat.GenericDefault, out _, out var lines);
        return lines;
    }

    /// <summary>
    /// A picture or an animated GIF, fitted inside the screen and dithered to black and white (Floyd–Steinberg).
    /// Returns every frame with how long it stays; at most 150 frames.
    /// </summary>
    public static List<(byte[] Bits, int DelayMs)> Picture(byte[] file)
    {
        using var ms = new MemoryStream(file);
        using var img = Image.FromStream(ms);
        var frames = new List<(byte[], int)>();
        var dim = new FrameDimension(img.FrameDimensionsList[0]);
        var count = Math.Min(150, img.GetFrameCount(dim));
        var delays = img.PropertyIdList.Contains(0x5100) ? img.GetPropertyItem(0x5100)!.Value : null;
        for (var i = 0; i < count; i++)
        {
            img.SelectActiveFrame(dim, i);
            var delay = delays is { Length: > 0 } && delays.Length >= (i + 1) * 4 ? BitConverter.ToInt32(delays, i * 4) * 10 : 100;
            frames.Add((Dither(img), Math.Max(40, delay)));
        }
        return frames;
    }

    private static byte[] Dither(Image img)
    {
        using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var scale = Math.Min((double)W / img.Width, (double)H / img.Height);
            var w = (int)Math.Round(img.Width * scale);
            var h = (int)Math.Round(img.Height * scale);
            g.DrawImage(img, (W - w) / 2, (H - h) / 2, w, h);
        }
        var lum = new double[W * H];
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var c = bmp.GetPixel(x, y);
                lum[y * W + x] = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) * c.A / 255.0;
            }
        var bits = new byte[W * H / 8];
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                var i = y * W + x;
                var on = lum[i] >= 128;
                if (on) bits[y * 16 + x / 8] |= (byte)(0x80 >> (x % 8));
                var err = lum[i] - (on ? 255 : 0);
                if (x + 1 < W) lum[i + 1] += err * 7 / 16;
                if (y + 1 < H)
                {
                    if (x > 0) lum[i + W - 1] += err * 3 / 16;
                    lum[i + W] += err * 5 / 16;
                    if (x + 1 < W) lum[i + W + 1] += err / 16;
                }
            }
        return bits;
    }

    /// <summary>The bits back as a picture, 4× larger: for the page's preview and for checking by eye.</summary>
    public static Bitmap ToBitmap(byte[] bits, int scale = 4)
    {
        var bmp = new Bitmap(W * scale, H * scale);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Black);
        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                if ((bits[y * 16 + x / 8] & (0x80 >> (x % 8))) != 0) g.FillRectangle(Brushes.White, x * scale, y * scale, scale, scale);
        return bmp;
    }
}
