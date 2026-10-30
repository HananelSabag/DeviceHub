using DeviceHub.Lighting;

namespace DeviceHub.Tests;

/// <summary>The newer keyboard effects and screens: what each one should look like at a glance.</summary>
public class MoreEffectsTests
{
    private static KeyboardLighting L(string effect) =>
        new() { Enabled = true, Effect = effect, Main = "#FFFFFF", Second = "#000000", Speed = 5, Brightness = 100 };

    [Theory]
    [InlineData(40, 30, 91, 255)]    // cool: blue
    [InlineData(62, 43, 234, 90)]    // warm: green
    [InlineData(95, 255, 42, 42)]    // hot: red
    public void Cpu_heat_colours(int celsius, byte r, byte g, byte b)
    {
        Assert.Equal(new Rgb(r, g, b), KeyboardEffects.Heat(celsius));
        Assert.Null(KeyboardEffects.Heat(null));
    }

    [Fact]
    public void Temperature_effect_paints_the_whole_keyboard_one_colour()
    {
        var f = KeyboardEffects.Frame(L("temperature"), 0, temperature: 95);
        Assert.All(f.Values, c => Assert.Equal(new Rgb(255, 42, 42), c));
        Assert.True(KeyboardEffects.FollowsHeat(L("temperature")));
        Assert.False(KeyboardEffects.Animated(L("temperature"))); // a new colour when the heat moves, not 20 frames a second
    }

    [Fact]
    public void Fire_burns_hotter_on_the_bottom_row()
    {
        double Bottom(double t) => KeyboardEffects.Frame(L("fire"), t)[0x2C].R;   // space bar
        double Top(double t) => KeyboardEffects.Frame(L("fire"), t)[0x3D].R;      // F4
        var times = Enumerable.Range(0, 40).Select(i => i * 0.13).ToList();
        Assert.True(times.Average(Bottom) > times.Average(Top) + 40);
    }

    [Fact]
    public void Rain_falls_from_the_top_row_before_the_bottom()
    {
        // Somewhere in a fall the top of a column lights before its bottom: find the first lit moment of each.
        double FirstLit(byte code)
        {
            for (var t = 0.0; t < 3; t += 0.02)
                if (KeyboardEffects.Frame(L("rain"), t)[code].R > 128) return t;
            return double.MaxValue;
        }
        Assert.True(FirstLit(0x1E) < FirstLit(0x1D)); // 1 (number row) before Z, same column
    }

    [Fact]
    public void Sound_meter_rises_with_the_level()
    {
        int Lit(float level) => KeyboardEffects.Frame(L("visualizer"), 0.3, level).Values.Count(c => c.R > 40);
        Assert.True(Lit(0) < Lit(0.4f));
        Assert.True(Lit(0.4f) < Lit(1f));
    }

    [Fact]
    public void Starlight_and_aurora_change_over_time()
    {
        foreach (var effect in new[] { "starlight", "aurora", "rain", "fire" })
        {
            Assert.True(KeyboardEffects.Animated(L(effect)));
            var a = KeyboardEffects.Frame(L(effect), 1.0);
            var b = KeyboardEffects.Frame(L(effect), 2.7);
            Assert.Contains(a.Keys, k => a[k] != b[k]);
        }
    }

    [Fact]
    public void New_screens_play()
    {
        foreach (var mode in new[] { "life", "fireworks", "bounce" })
        {
            var anim = ScreenAnimation.For(mode)!;
            var lit = 0;
            for (var t = 0.0; t < 6; t += 0.05) lit += anim.Frame(t, 0).Count(b => b != 0);
            Assert.True(lit > 0, mode);
        }
        Assert.Contains(OledScreen.Analog(new DateTime(2026, 10, 30, 21, 16, 40), english: false), b => b != 0);
    }

    [Fact]
    public void Life_keeps_going_when_a_world_dies_out()
    {
        // A minute and a half of generations: the screen is never blank for long, because a dead world reseeds.
        var anim = ScreenAnimation.For("life")!;
        var blankRun = 0;
        for (var t = 0.0; t < 90; t += 0.1)
        {
            blankRun = anim.Frame(t, 0).All(b => b == 0) ? blankRun + 1 : 0;
            Assert.True(blankRun < 10);
        }
    }
}
