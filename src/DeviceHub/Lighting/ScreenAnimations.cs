using System.Drawing;

namespace DeviceHub.Lighting;

/// <summary>
/// Moving pictures for the keyboard's 128 × 40 screen, drawn 20 times a second. All of them are drawn here,
/// from scratch: warp-speed stars, falling code, an energy blast, bars that follow the sound, and a ball heading
/// for the goal. Each keeps its own state between frames.
/// </summary>
internal abstract class ScreenAnimation
{
    protected readonly Random Rng = new();
    private double last = -1;

    public static ScreenAnimation? For(string mode) => mode switch
    {
        "stars" => new Stars(),
        "matrix" => new Matrix(),
        "energy" => new Energy(),
        "sound" => new SoundBars(),
        "goal" => new Goal(),
        _ => null,
    };

    /// <summary>The next frame. t: seconds since the animation started; level: the sound level 0-1.</summary>
    public byte[] Frame(double t, float level)
    {
        var dt = last < 0 ? 0.05 : Math.Clamp(t - last, 0, 0.2);
        last = t;
        var (bmp, g) = OledScreen.Canvas();
        using (bmp) using (g)
        {
            Draw(g, t, dt, level);
            return OledScreen.Pack(bmp);
        }
    }

    protected abstract void Draw(Graphics g, double t, double dt, float level);

    protected static void Dot(Graphics g, double x, double y)
    {
        if (x is >= 0 and < OledScreen.W && y is >= 0 and < OledScreen.H) g.FillRectangle(Brushes.White, (int)x, (int)y, 1, 1);
    }

    /// <summary>Warp speed: stars come at you from the middle, stretching into lines as they pass.</summary>
    private sealed class Stars : ScreenAnimation
    {
        private readonly (double X, double Y, double Z)[] stars = new (double, double, double)[80];
        public Stars() { for (var i = 0; i < stars.Length; i++) stars[i] = New(Rng.NextDouble() * 4); }
        private (double, double, double) New(double z) => ((Rng.NextDouble() - 0.5) * 6, (Rng.NextDouble() - 0.5) * 2.2, z);

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            var speed = 1.6 + level * 4; // louder music, faster travel
            for (var i = 0; i < stars.Length; i++)
            {
                var (x, y, z) = stars[i];
                var nz = z - speed * dt;
                if (nz <= 0.08) { stars[i] = New(4); continue; }
                double Px(double zz) => 64 + x / zz * 22;
                double Py(double zz) => 20 + y / zz * 22;
                var (x0, y0, x1, y1) = (Px(z), Py(z), Px(nz), Py(nz));
                if (x1 is < 0 or >= 128 || y1 is < 0 or >= 40) { stars[i] = New(4); continue; }
                if (nz < 1.4) g.DrawLine(Pens.White, (float)x0, (float)y0, (float)x1, (float)y1);
                else Dot(g, x1, y1);
                stars[i] = (x, y, nz);
            }
        }
    }

    /// <summary>Falling code: columns of tiny glyphs, a bright head and a thinning trail.</summary>
    private sealed class Matrix : ScreenAnimation
    {
        private readonly double[] head = new double[21];
        private readonly double[] speed = new double[21];
        private readonly int[,] glyph = new int[21, 8];
        public Matrix()
        {
            for (var c = 0; c < head.Length; c++) { head[c] = -Rng.NextDouble() * 40; speed[c] = 14 + Rng.NextDouble() * 22; }
            for (var c = 0; c < 21; c++) for (var r = 0; r < 8; r++) glyph[c, r] = Rng.Next(1 << 15);
        }

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            for (var c = 0; c < head.Length; c++)
            {
                head[c] += speed[c] * dt;
                if (head[c] - 40 > 40) { head[c] = -Rng.NextDouble() * 20; speed[c] = 14 + Rng.NextDouble() * 22; }
                if (Rng.NextDouble() < 0.08) glyph[c, Rng.Next(8)] = Rng.Next(1 << 15);
                var cellHead = (int)Math.Floor(head[c] / 6);
                for (var k = 0; k < 7; k++)
                {
                    var row = cellHead - k;
                    if (row is < 0 or > 6) continue;
                    // The trail thins out: every pixel at the head, fewer further up.
                    var keep = k == 0 ? 1.0 : 0.75 - k * 0.1;
                    var bits = glyph[c, (row % 8 + 8) % 8];
                    for (var b = 0; b < 15; b++)
                        if ((bits >> b & 1) == 1 && (k == 0 || ((b * 7 + row * 3 + k) % 10) / 10.0 < keep))
                            Dot(g, c * 6 + 1 + b % 3, row * 6 + b / 3);
                }
            }
        }
    }

    /// <summary>An energy blast: sparks gather into a growing ball, a beam fires across, the far side explodes.</summary>
    private sealed class Energy : ScreenAnimation
    {
        private const double Cycle = 4.6;
        private readonly List<(double X, double Y, double Vx, double Vy)> sparks = [];

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            var p = t % Cycle;
            const float bx = 22, by = 20;
            if (p < 2.6)
            {
                // Charging: the ball grows, sparks rush in, small bolts flicker round it.
                var r = (float)(2 + p / 2.6 * 8 + Math.Sin(t * 30) * 0.8);
                g.FillEllipse(Brushes.White, bx - r, by - r, r * 2, r * 2);
                g.DrawEllipse(Pens.White, bx - r - 3, by - r - 3, r * 2 + 6, r * 2 + 6);
                for (var i = 0; i < 14; i++)
                {
                    var a = i * 0.45 + t * 1.3;
                    var d = 30 - (t * 40 + i * 9) % 30 + r;
                    Dot(g, bx + Math.Cos(a) * d * 1.6, by + Math.Sin(a) * d * 0.9);
                }
                if (Rng.NextDouble() < 0.6) Bolt(g, bx, by, r + 2);
            }
            else if (p < 3.3)
            {
                // The beam: thick, with a wavering edge, reaching the right side.
                var reach = (float)Math.Min(1, (p - 2.6) / 0.25) * (128 - bx);
                for (var x = 0; x < reach; x++)
                {
                    var half = 5 + Math.Sin(x * 0.45 + t * 40) * 1.5;
                    g.FillRectangle(Brushes.White, bx + x, (float)(by - half), 1, (float)(half * 2));
                }
                g.FillEllipse(Brushes.White, bx - 11, by - 11, 22, 22);
                if (p > 3.05 && sparks.Count == 0)
                    for (var i = 0; i < 40; i++) { var a = Rng.NextDouble() * Math.Tau; var s = 20 + Rng.NextDouble() * 60; sparks.Add((118, by, Math.Cos(a) * s, Math.Sin(a) * s)); }
            }
            else if (p < 3.45) g.FillRectangle(Brushes.White, 0, 0, 128, 40); // the flash
            if (p >= 3.05)
            {
                // The explosion's sparks fly out and fade.
                for (var i = sparks.Count - 1; i >= 0; i--)
                {
                    var (x, y, vx, vy) = sparks[i];
                    x += vx * dt; y += vy * dt; vy += 30 * dt;
                    sparks[i] = (x, y, vx * 0.97, vy * 0.97);
                    if (p < 3.45 || Rng.NextDouble() < 1.2 - (p - 3.3)) Dot(g, x, y);
                }
                if (p > 4.4) sparks.Clear();
                var ring = (float)((p - 3.3) * 60);
                if (p is > 3.3 and < 4.0) g.DrawEllipse(Pens.White, 118 - ring, by - ring * 0.6f, ring * 2, ring * 1.2f);
            }
        }

        private void Bolt(Graphics g, float cx, float cy, float r)
        {
            var a = Rng.NextDouble() * Math.Tau;
            float x = cx + (float)Math.Cos(a) * r, y = cy + (float)Math.Sin(a) * r;
            for (var i = 0; i < 4; i++)
            {
                float nx = x + (float)Math.Cos(a) * 4 + Rng.Next(-2, 3), ny = y + (float)Math.Sin(a) * 3 + Rng.Next(-2, 3);
                g.DrawLine(Pens.White, x, y, nx, ny);
                (x, y) = (nx, ny);
            }
        }
    }

    /// <summary>The sound as it plays: the last few seconds of level as bars, mirrored round the middle.</summary>
    private sealed class SoundBars : ScreenAnimation
    {
        private readonly float[] history = new float[42];
        private int next;

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            history[next] = level;
            next = (next + 1) % history.Length;
            for (var i = 0; i < history.Length; i++)
            {
                var v = history[(next + i) % history.Length];
                var half = Math.Max(1, (int)Math.Round(Math.Sqrt(v) * 19));
                g.FillRectangle(Brushes.White, i * 3 + 1, 20 - half, 2, half * 2);
            }
        }
    }

    /// <summary>For the Rocket League player: a ball on boost bounces up the pitch and into the goal.</summary>
    private sealed class Goal : ScreenAnimation
    {
        private double x = 8, y = 20, vx = 70, vy = 38;
        private double scored = -1;
        private readonly List<(double X, double Y, double Life)> trail = [];

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            // The goal: an open frame on the right.
            g.DrawLine(Pens.White, 120, 10, 127, 10);
            g.DrawLine(Pens.White, 120, 30, 127, 30);
            g.DrawLine(Pens.White, 127, 10, 127, 30);
            if (scored >= 0)
            {
                var since = t - scored;
                if (since < 1.6)
                {
                    using var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel);
                    if ((int)(since * 6) % 2 == 0) g.DrawString("GOAL!", font, Brushes.White, 30, 8);
                    for (var i = 0; i < 16; i++) Dot(g, 118 - since * 50 * Math.Abs(Math.Cos(i)), 20 + since * 40 * Math.Sin(i * 2.1));
                    return;
                }
                (x, y, vx, vy, scored) = (8, 8 + Rng.NextDouble() * 24, 60 + Rng.NextDouble() * 30, (Rng.NextDouble() - 0.5) * 90, -1);
            }
            x += vx * dt; y += vy * dt;
            if (y < 3 || y > 37) { vy = -vy; y = Math.Clamp(y, 3, 37); }
            if (x > 118 && y is > 11 and < 29) { scored = t; trail.Clear(); return; }
            if (x > 124) { vx = -Math.Abs(vx); x = 124; }
            if (x < 3) vx = Math.Abs(vx);
            trail.Add((x - Math.Sign(vx) * 4, y, 1));
            for (var i = trail.Count - 1; i >= 0; i--)
            {
                var (tx, ty, life) = trail[i];
                life -= dt * 2.5;
                if (life <= 0) { trail.RemoveAt(i); continue; }
                trail[i] = (tx, ty + (Rng.NextDouble() - 0.5), life);
                if (Rng.NextDouble() < life) Dot(g, tx, ty);
            }
            g.FillEllipse(Brushes.White, (float)x - 3, (float)y - 3, 7, 7);
            g.FillRectangle(Brushes.Black, (float)x - 1, (float)y - 1, 1, 1); // the ball's panel line
        }
    }
}
