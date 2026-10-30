using System.Drawing;

namespace DeviceHub.Lighting;

/// <summary>
/// Moving pictures for the keyboard's 128 × 40 screen, drawn 20 times a second. All of them are drawn here,
/// from scratch: warp-speed stars, falling code, an energy blast, bars that follow the sound, a ball heading
/// for the goal, a power-up, and a show that plays them all with transitions. Each keeps its own state between frames.
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
        "power" => new PowerUp(),
        "life" => new Life(),
        "fireworks" => new Fireworks(),
        "bounce" => new Bounce(),
        "show" => new Show(),
        _ => null,
    };

    /// <summary>The next frame. t: seconds since the animation started; level: the sound level 0-1.</summary>
    public virtual byte[] Frame(double t, float level)
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

    /// <summary>
    /// A power-up: an aura of rising sparks grows round a point, the screen starts to shake, a power reading races
    /// up past 9000, then a flash and a shockwave; the aura stays, burning, before it all starts again.
    /// </summary>
    private sealed class PowerUp : ScreenAnimation
    {
        private const double Cycle = 7.0, Charge = 3.6;
        private readonly List<(double X, double Y, double Vy, double Life)> sparks = [];

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            var p = t % Cycle;
            var power = p < Charge ? Math.Pow(p / Charge, 2.2) : 1;
            // The shake: nothing at first, a pixel or two as the power builds.
            var shake = p is > 1.6 and < 4.2 ? Rng.Next(-1, 2) * (p < Charge ? 1 : 2) : 0;
            g.TranslateTransform(shake, Rng.Next(-1, 2) * Math.Abs(shake) / 2f);

            // The aura: sparks rise from a growing base and flicker out.
            var width = 6 + power * 34;
            var spawn = (int)(3 + power * 14);
            for (var i = 0; i < spawn; i++)
                sparks.Add((64 + (Rng.NextDouble() - 0.5) * width * 2, 39, -(18 + Rng.NextDouble() * 40 * (0.4 + power)), 0.5 + Rng.NextDouble() * 0.7));
            for (var i = sparks.Count - 1; i >= 0; i--)
            {
                var (x, y, vy, life) = sparks[i];
                life -= dt;
                if (life <= 0 || y < -2) { sparks.RemoveAt(i); continue; }
                // Flames lean in towards the middle as they rise.
                x += (64 - x) * dt * 0.9 + (Rng.NextDouble() - 0.5) * 1.5;
                y += vy * dt;
                sparks[i] = (x, y, vy, life);
                Dot(g, x, y);
                if (life > 0.4) Dot(g, x, y + 1);
            }
            // A bright core.
            var core = (float)(3 + power * 6 + Math.Sin(t * 25) * 0.8);
            g.FillEllipse(Brushes.White, 64 - core, 33 - core * 0.6f, core * 2, core * 1.2f);
            if (p > 1.2 && Rng.NextDouble() < 0.25 + power * 0.4) Crackle(g, 64 + Rng.Next(-30, 30), Rng.Next(4, 30));

            g.ResetTransform();
            // The reading races up, then holds past 9000.
            var reading = p < Charge ? (int)(9001 * power) : 9001;
            using var font = new Font("Bahnschrift", 11, FontStyle.Bold, GraphicsUnit.Pixel);
            g.FillRectangle(Brushes.Black, 0, 0, 44, 13);
            g.DrawString(reading.ToString(), font, Brushes.White, 1, 0);
            if (p is >= Charge and < Charge + 0.18) g.FillRectangle(Brushes.White, 0, 0, 128, 40); // the flash
            if (p is >= Charge + 0.18 and < Charge + 1.2)
            {
                var r = (float)((p - Charge) * 120);
                g.DrawEllipse(Pens.White, 64 - r, 30 - r * 0.45f, r * 2, r * 0.9f);
            }
            if (p >= Charge + 0.3 && (int)(t * 4) % 2 == 0) g.DrawString("!!", font, Brushes.White, 46, 0);
        }

        private void Crackle(Graphics g, float x, float y)
        {
            for (var i = 0; i < 3; i++)
            {
                float nx = x + Rng.Next(-4, 5), ny = y + Rng.Next(2, 5);
                g.DrawLine(Pens.White, x, y, nx, ny);
                (x, y) = (nx, ny);
            }
        }
    }

    /// <summary>
    /// The show: every animation in turn, nine seconds each, joined by a different transition every time
    /// (a wipe, a dissolve, an iris, blinds, a slide).
    /// </summary>
    internal sealed class Show : ScreenAnimation
    {
        private static readonly string[] Acts = ["stars", "energy", "matrix", "fireworks", "goal", "life", "power", "bounce", "sound"];
        private const double Act = 9, Blend = 0.9;
        private ScreenAnimation? current, next;
        private int index = -1;

        public override byte[] Frame(double t, float level)
        {
            var act = (int)(t / Act);
            var within = t - act * Act;
            if (act != index)
            {
                current = next ?? For(Acts[act % Acts.Length]);
                next = null;
                index = act;
            }
            var a = current!.Frame(t, level);
            if (within < Act - Blend) return a;
            next ??= For(Acts[(act + 1) % Acts.Length]);
            var b = next!.Frame(t, level);
            return Transition(a, b, (within - (Act - Blend)) / Blend, act % 5);
        }

        protected override void Draw(Graphics g, double t, double dt, float level) { }

        /// <summary>From picture a to picture b, p of the way (0-1), one of five ways.</summary>
        internal static byte[] Transition(byte[] a, byte[] b, double p, int kind)
        {
            var o = new byte[640];
            var shift = (int)Math.Round(p * 128);
            for (var y = 0; y < 40; y++)
                for (var x = 0; x < 128; x++)
                {
                    bool fromB;
                    var (sx, src) = (x, a);
                    switch (kind)
                    {
                        case 0: fromB = x < p * 128; break;                                         // wipe
                        case 1: fromB = (unchecked((uint)(x * 73 + y * 151) * 2654435761u) >> 24) / 256.0 < p; break; // dissolve
                        case 2: fromB = Math.Sqrt((x - 64) * (x - 64) + (y - 20) * (y - 20) * 4) < p * 76; break; // iris (76: the far corners)
                        case 3: fromB = x % 16 < p * 16; break;                                     // blinds
                        default:                                                                    // slide
                            fromB = x >= 128 - shift;
                            sx = fromB ? x - (128 - shift) : x + shift;
                            break;
                    }
                    if (fromB) src = b;
                    if ((src[y * 16 + sx / 8] & (0x80 >> (sx % 8))) != 0) o[y * 16 + x / 8] |= (byte)(0x80 >> (x % 8));
                }
            return o;
        }
    }

    /// <summary>
    /// Conway's Game of Life on 2 × 2 pixel cells, ten generations a second. When it settles into stillness or a
    /// loop, or after a minute, a fresh random world begins.
    /// </summary>
    private sealed class Life : ScreenAnimation
    {
        private const int Cols = 64, Rows = 20;
        private bool[,] cells = new bool[Cols, Rows];
        private readonly Queue<int> recent = new();
        private double nextStep, born;

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            if (t >= nextStep)
            {
                if (t - born > 60 || born == 0 && nextStep == 0) Seed(t);
                Step();
                var hash = Hash();
                if (recent.Contains(hash)) Seed(t);
                recent.Enqueue(hash);
                if (recent.Count > 6) recent.Dequeue();
                nextStep = t + 0.1;
            }
            for (var x = 0; x < Cols; x++)
                for (var y = 0; y < Rows; y++)
                    if (cells[x, y]) g.FillRectangle(Brushes.White, x * 2, y * 2, 2, 2);
        }

        private void Seed(double t)
        {
            for (var x = 0; x < Cols; x++)
                for (var y = 0; y < Rows; y++)
                    cells[x, y] = Rng.NextDouble() < 0.32;
            recent.Clear();
            born = t;
        }

        private void Step()
        {
            var next = new bool[Cols, Rows];
            for (var x = 0; x < Cols; x++)
                for (var y = 0; y < Rows; y++)
                {
                    var n = 0;
                    for (var dx = -1; dx <= 1; dx++)
                        for (var dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && cells[(x + dx + Cols) % Cols, (y + dy + Rows) % Rows]) n++;
                    next[x, y] = n == 3 || (n == 2 && cells[x, y]);
                }
            cells = next;
        }

        private int Hash()
        {
            var h = 17;
            foreach (var c in cells) h = h * 31 + (c ? 1 : 0);
            return h;
        }
    }

    /// <summary>Fireworks: rockets climb with a trail, burst into a ring of sparks that fall and fade.</summary>
    private sealed class Fireworks : ScreenAnimation
    {
        private readonly List<(double X, double Y, double Vx, double Vy, double Life, bool Rocket)> parts = [];
        private double nextLaunch;

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            if (t >= nextLaunch)
            {
                parts.Add((14 + Rng.NextDouble() * 100, 40, (Rng.NextDouble() - 0.5) * 12, -(48 + Rng.NextDouble() * 18), 0.5 + Rng.NextDouble() * 0.35, true));
                nextLaunch = t + 0.5 + Rng.NextDouble() * 0.9;
            }
            for (var i = parts.Count - 1; i >= 0; i--)
            {
                var (x, y, vx, vy, life, rocket) = parts[i];
                life -= dt;
                if (rocket && life <= 0)
                {
                    // The burst: a ring of sparks, some faster than others.
                    var n = 18 + Rng.Next(12);
                    for (var k = 0; k < n; k++)
                    {
                        var a = k * Math.Tau / n;
                        var s = 14 + Rng.NextDouble() * 10;
                        parts.Add((x, y, Math.Cos(a) * s, Math.Sin(a) * s * 0.8, 0.7 + Rng.NextDouble() * 0.6, false));
                    }
                    parts.RemoveAt(i);
                    continue;
                }
                if (life <= 0) { parts.RemoveAt(i); continue; }
                vy += (rocket ? 30 : 18) * dt;
                x += vx * dt; y += vy * dt;
                parts[i] = (x, y, vx * (rocket ? 1 : 0.985), vy, life, rocket);
                if (rocket) { Dot(g, x, y); Dot(g, x, y + 1); if (Rng.NextDouble() < 0.6) Dot(g, x - vx * 0.03, y + 3); }
                else if (life > 0.25 || Rng.NextDouble() < life * 4) Dot(g, x, y);
            }
        }
    }

    /// <summary>The old DVD screensaver, with DeviceHub's name: it bounces off the edges and flashes when it hits a corner.</summary>
    private sealed class Bounce : ScreenAnimation
    {
        private double x = 10, y = 6, vx = 26, vy = 14;
        private double corner = -10;

        protected override void Draw(Graphics g, double t, double dt, float level)
        {
            using var font = new Font("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Pixel);
            const int w = 64, h = 16;
            x += vx * dt; y += vy * dt;
            bool hitX = false, hitY = false;
            if (x <= 0 || x + w >= 128) { vx = -vx; x = Math.Clamp(x, 0, 128 - w); hitX = true; }
            if (y <= 0 || y + h >= 40) { vy = -vy; y = Math.Clamp(y, 0, 40 - h); hitY = true; }
            if (hitX && hitY) corner = t;
            var flash = t - corner < 0.6 && (int)((t - corner) * 10) % 2 == 0;
            if (flash) g.FillRectangle(Brushes.White, (float)x, (float)y, w, h);
            else g.DrawRectangle(Pens.White, (float)x, (float)y, w - 1, h - 1);
            g.DrawString("DeviceHub", font, flash ? Brushes.Black : Brushes.White, (float)x + 3, (float)y + 1);
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
