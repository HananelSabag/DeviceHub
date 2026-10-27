using System.Globalization;

namespace DeviceHub.Lighting;

/// <summary>
/// What DeviceHub shows on the keyboard. Off: the keyboard's own saved lighting. On: one of the effects below,
/// computed here and sent as a colour for every key (the Apex 5 has no effects of its own that can be set).
/// </summary>
internal sealed record KeyboardLighting
{
    public bool Enabled { get; init; }
    /// <summary>static | breathing | spectrum | wave | gradient | music | reactive | ripple</summary>
    public string Effect { get; init; } = "static";
    public string Main { get; init; } = "#FF6A1A";
    /// <summary>W A S D and the arrows in their own colour (static and breathing); null for none.</summary>
    public string? Accent { get; init; }
    /// <summary>The far end of the gradient.</summary>
    public string Second { get; init; } = "#1E5BFF";
    /// <summary>1 slow … 10 fast.</summary>
    public int Speed { get; init; } = 5;
    /// <summary>0-100 %.</summary>
    public int Brightness { get; init; } = 100;
}

internal readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Parse(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, null, out var v)
            ? new((byte)(v >> 16), (byte)(v >> 8), (byte)v) : new(255, 255, 255);

    public Rgb Scale(double f) => new((byte)Math.Round(R * f), (byte)Math.Round(G * f), (byte)Math.Round(B * f));

    public static Rgb Lerp(Rgb a, Rgb b, double t) =>
        new((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    /// <summary>Hue 0-1 at full saturation and value.</summary>
    public static Rgb Hue(double h)
    {
        h = (h % 1 + 1) % 1 * 6;
        var x = 1 - Math.Abs(h % 2 - 1);
        var (r, g, b) = (int)h switch { 0 => (1d, x, 0d), 1 => (x, 1d, 0d), 2 => (0d, 1d, x), 3 => (0d, x, 1d), 4 => (x, 0d, 1d), _ => (1d, 0d, x) };
        return new((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }
}

internal static class KeyboardEffects
{
    /// <summary>Effects that change over time and need frames; the others are sent once.</summary>
    public static bool Animated(KeyboardLighting l) => l.Enabled && l.Effect is "breathing" or "spectrum" or "wave" or "music" or "reactive" or "ripple";

    /// <summary>Effects that answer key presses (and so need to know which keys are pressed).</summary>
    public static bool WantsKeys(KeyboardLighting l) => l.Enabled && l.Effect is "reactive" or "ripple";

    /// <summary>A key going down at a moment (seconds, on the same clock as t).</summary>
    public readonly record struct Press(byte Code, double T);

    /// <summary>How long a press shows: 0.5 s fast … 1.8 s slow.</summary>
    public static double Fade(KeyboardLighting l) => 1.95 - Math.Clamp(l.Speed, 1, 10) * 0.145;

    private static readonly Dictionary<byte, (double X, double Y)> centers =
        KeyboardLayout.Keys.ToDictionary(k => k.Code, k => (k.X + k.W / 2, k.Y + k.H / 2));

    /// <summary>A colour for every key at a moment in time (seconds), brightness applied. level: the sound, 0-1 (music).</summary>
    public static Dictionary<byte, Rgb> Frame(KeyboardLighting l, double t, float level = 0, IReadOnlyList<Press>? presses = null)
    {
        var main = Rgb.Parse(l.Main);
        var accent = l.Accent is null ? (Rgb?)null : Rgb.Parse(l.Accent);
        var second = Rgb.Parse(l.Second);
        var bright = Math.Clamp(l.Brightness, 0, 100) / 100.0;
        var speed = Math.Clamp(l.Speed, 1, 10);
        var colors = new Dictionary<byte, Rgb>(KeyboardLayout.Keys.Count);
        foreach (var k in KeyboardLayout.Keys)
        {
            var x = (k.X + k.W / 2) / KeyboardLayout.Width;
            var own = accent is { } a && KeyboardLayout.Accent.Contains(k.Code) ? a : main;
            var c = l.Effect switch
            {
                // A slow breath: 1.5 s … 8 s a cycle, never quite dark so the keys stay readable.
                "breathing" => own.Scale(0.12 + 0.88 * Math.Pow(Math.Sin(Math.PI * t / (8.5 - speed * 0.7)), 2)),
                "spectrum" => Rgb.Hue(t * speed * 0.02),
                "wave" => Rgb.Hue(x * 0.85 - t * speed * 0.06),
                "gradient" => Rgb.Lerp(main, second, x),
                // The keys follow the sound: dim in silence, full on every beat; the accent keys stay lit.
                "music" => accent is not null && KeyboardLayout.Accent.Contains(k.Code) ? own : own.Scale(0.08 + 0.92 * Math.Min(1, level * 1.4)),
                // Key presses, over a background in the second colour: the key itself, or a ring spreading from it.
                "reactive" => Rgb.Lerp(second.Scale(0.35), main, Reactive(k.Code, t, presses, Fade(l))),
                "ripple" => Rgb.Lerp(second.Scale(0.35), main, Ripple(k, t, presses, Fade(l), speed)),
                _ => own,
            };
            colors[k.Code] = c.Scale(bright);
        }
        return colors;
    }

    /// <summary>1 the moment the key goes down, fading to 0.</summary>
    private static double Reactive(byte code, double t, IReadOnlyList<Press>? presses, double fade)
    {
        var v = 0.0;
        if (presses is null) return v;
        foreach (var p in presses)
            if (p.Code == code && t >= p.T) v = Math.Max(v, 1 - (t - p.T) / fade);
        return Math.Clamp(v, 0, 1);
    }

    /// <summary>A ring about one key wide, spreading from the pressed key and fading as it goes.</summary>
    private static double Ripple(KeyboardLayout.Key k, double t, IReadOnlyList<Press>? presses, double fade, int speed)
    {
        var v = 0.0;
        if (presses is null) return v;
        var (kx, ky) = (k.X + k.W / 2, k.Y + k.H / 2);
        foreach (var p in presses)
        {
            if (!centers.TryGetValue(p.Code, out var c) || t < p.T) continue;
            var age = t - p.T;
            var life = fade * 1.6;
            if (age > life) continue;
            var radius = age * (6 + speed * 1.8);
            var distance = Math.Sqrt((kx - c.X) * (kx - c.X) + (ky - c.Y) * (ky - c.Y) * 1.3);
            var ring = 1 - Math.Abs(distance - radius) / 1.3;
            var hit = Math.Max(ring, p.Code == k.Code ? 1 - age / fade : 0);
            v = Math.Max(v, hit * (1 - age / life));
        }
        return Math.Clamp(v, 0, 1);
    }
}
