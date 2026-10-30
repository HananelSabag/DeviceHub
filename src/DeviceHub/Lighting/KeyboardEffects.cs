using System.Globalization;

namespace DeviceHub.Lighting;

/// <summary>
/// What DeviceHub shows on the keyboard. Off: the keyboard's own saved lighting. On: one of the effects below,
/// computed here and sent as a colour for every key (the Apex 5 has no effects of its own that can be set).
/// </summary>
internal sealed record KeyboardLighting
{
    public bool Enabled { get; init; }
    /// <summary>static | breathing | spectrum | wave | gradient | music | reactive | ripple | starlight | rain | fire | aurora | visualizer | temperature</summary>
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
    public static bool Animated(KeyboardLighting l) => l.Enabled && l.Effect is "breathing" or "spectrum" or "wave" or "music" or "reactive" or "ripple"
        or "starlight" or "rain" or "fire" or "aurora" or "visualizer";

    /// <summary>The temperature effect changes with the CPU's heat: a new colour every couple of seconds, not every frame.</summary>
    public static bool FollowsHeat(KeyboardLighting l) => l.Enabled && l.Effect == "temperature";

    /// <summary>Effects that answer key presses (and so need to know which keys are pressed).</summary>
    public static bool WantsKeys(KeyboardLighting l) => l.Enabled && l.Effect is "reactive" or "ripple";

    /// <summary>A key going down at a moment (seconds, on the same clock as t).</summary>
    public readonly record struct Press(byte Code, double T);

    /// <summary>How long a press shows: 0.5 s fast … 1.8 s slow.</summary>
    public static double Fade(KeyboardLighting l) => 1.95 - Math.Clamp(l.Speed, 1, 10) * 0.145;

    private static readonly Dictionary<byte, (double X, double Y)> centers =
        KeyboardLayout.Keys.ToDictionary(k => k.Code, k => (k.X + k.W / 2, k.Y + k.H / 2));

    /// <summary>A colour for every key at a moment in time (seconds), brightness applied. level: the sound, 0-1 (music).</summary>
    public static Dictionary<byte, Rgb> Frame(KeyboardLighting l, double t, float level = 0, IReadOnlyList<Press>? presses = null, int? temperature = null)
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
                // Stars: keys glint in the main colour, one at a time, over a faint background.
                "starlight" => Rgb.Lerp(second.Scale(0.12), main, Math.Pow(Math.Max(0, Math.Sin((t * (0.25 + speed * 0.07) + Seed(k.Code) * 7.3) * Math.Tau)), 12)),
                "rain" => Rgb.Lerp(second.Scale(0.1), main, Rain(k, t, speed)),
                "fire" => Fire((k.Y + k.H / 2) / KeyboardLayout.Height, x, t, speed, Seed(k.Code)),
                // Aurora: the two colours flowing into each other in slow waves.
                "aurora" => Rgb.Lerp(main, second, 0.5 + 0.5 * Math.Sin(x * 5 + t * speed * 0.25 + Math.Sin(k.Y * 1.3 + t * 0.6) * 1.5))
                    .Scale(0.65 + 0.35 * Math.Sin(x * 3 - t * 0.4)),
                "visualizer" => Meter(k, x, t, level, main, second),
                "temperature" => Heat(temperature) ?? own,
                _ => own,
            };
            colors[k.Code] = c.Scale(bright);
        }
        return colors;
    }

    /// <summary>A fixed number 0-1 for each key, so effects look random but stay the same from frame to frame.</summary>
    private static double Seed(byte code) => unchecked((uint)(code * 2654435761u)) % 1000 / 1000.0;

    /// <summary>Drops falling down each column of keys, every column at its own moment.</summary>
    private static double Rain(KeyboardLayout.Key k, double t, int speed)
    {
        var column = (int)(k.X + k.W / 2);
        var period = 2.3 - speed * 0.16;
        var head = ((t / period + Seed((byte)(column * 7 + 3))) % 1.5) * (KeyboardLayout.Height + 2) - 1;
        var behind = head - (k.Y + k.H / 2);
        return behind is >= 0 and < 2.4 ? 1 - behind / 2.4 : 0;
    }

    /// <summary>Flames: hottest on the bottom rows, flickering, from dark red through orange to yellow.</summary>
    private static Rgb Fire(double y, double x, double t, int speed, double seed)
    {
        var flicker = 0.5 + 0.5 * Math.Sin(t * (3 + speed * 0.6) + x * 9.1 + seed * 5) * Math.Sin(t * 3.7 + x * 4.3 + seed * 2);
        var heat = Math.Clamp(y * 1.25 - 0.45 + flicker * 0.55, 0, 1);
        return heat < 0.4 ? Rgb.Lerp(new Rgb(0, 0, 0), new Rgb(170, 8, 0), heat / 0.4)
            : heat < 0.75 ? Rgb.Lerp(new Rgb(170, 8, 0), new Rgb(255, 100, 0), (heat - 0.4) / 0.35)
            : Rgb.Lerp(new Rgb(255, 100, 0), new Rgb(255, 210, 60), (heat - 0.75) / 0.25);
    }

    /// <summary>A level meter across the keyboard: columns rise from the bottom row with the sound, coloured by height.</summary>
    private static Rgb Meter(KeyboardLayout.Key k, double x, double t, float level, Rgb low, Rgb high)
    {
        var height = 1 - (k.Y + k.H / 2) / KeyboardLayout.Height;      // bottom row ≈ 0.1, function row ≈ 0.95
        var column = Math.Floor(x * 22);
        var reach = level * 1.35 * (0.7 + 0.3 * Math.Sin(column * 1.7 + t * 9));
        return height <= reach ? Rgb.Lerp(low, high, height) : low.Scale(0.06);
    }

    /// <summary>The CPU's heat as a colour: blue when cool, green, yellow, orange, red when hot (92 °C and up).</summary>
    internal static Rgb? Heat(int? celsius)
    {
        if (celsius is not { } c) return null;
        (double At, Rgb Color)[] stops = [(50, new(30, 91, 255)), (62, new(43, 234, 90)), (75, new(255, 208, 0)), (85, new(255, 106, 26)), (92, new(255, 42, 42))];
        if (c <= stops[0].At) return stops[0].Color;
        for (var i = 1; i < stops.Length; i++)
            if (c <= stops[i].At) return Rgb.Lerp(stops[i - 1].Color, stops[i].Color, (c - stops[i - 1].At) / (stops[i].At - stops[i - 1].At));
        return stops[^1].Color;
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
