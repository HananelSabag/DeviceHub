using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;
using DeviceHub.Interop;
using DeviceHub.Lighting;

namespace DeviceHub.Tests;

/// <summary>The bytes every change sends, checked against the protocols they come from (OpenRazer, HeadsetControl, OpenRGB, Linux hid-playstation).</summary>
public class ControlTests
{
    [Fact]
    public void Razer_set_packet_carries_its_arguments_and_crc()
    {
        var buf = RazerMouse.BuildPacket(0x1F, 0x04, 0x05, 0x07, [0x01, 0x3E, 0x80, 0x3E, 0x80, 0, 0]); // 16000 DPI
        Assert.Equal(0x04, buf[7]);
        Assert.Equal(0x05, buf[8]);
        Assert.Equal(0x01, buf[9]);  // saved in the mouse
        Assert.Equal(0x3E, buf[10]);
        Assert.Equal(0x80, buf[11]);
        Assert.Equal(RazerMouse.Crc(buf), buf[89]);
    }

    [Fact]
    public void Dpi_stages_as_this_mouse_reports_them()
    {
        // Recorded from the mouse: 5 stages 800/1800/4600/7200/16000, stage 5 active, numbered from 1.
        var args = RazerMouse.BuildStages([800, 1800, 4600, 7200, 16000], active: 4, firstNumber: 1);
        var packet = new byte[90];
        args.CopyTo(packet, 8);
        var back = RazerMouse.ParseStages(packet)!;
        Assert.Equal([800, 1800, 4600, 7200, 16000], back.Dpi);
        Assert.Equal(4, back.Active);
        Assert.Equal(1, back.FirstNumber);
        Assert.Equal(0x01, args[0]);   // saved
        Assert.Equal(5, args[1]);      // active stage number
        Assert.Equal(5, args[2]);      // count
        Assert.Equal(1, args[3]);      // first stage's number
        Assert.Equal(38, args.Length); // exactly the 0x26 bytes the command carries
    }

    [Fact]
    public void Dpi_stages_keep_a_zero_based_numbering_too()
    {
        var args = RazerMouse.BuildStages([400, 800], active: 1, firstNumber: 0);
        var packet = new byte[90];
        args.CopyTo(packet, 8);
        var back = RazerMouse.ParseStages(packet)!;
        Assert.Equal(1, back.Active);
        Assert.Equal(0, back.FirstNumber);
    }

    [Fact]
    public void Equalizer_bands_become_headsetcontrol_bytes()
    {
        // HeadsetControl's "Bass Boost" preset bytes for the Arctis 7+.
        var bytes = ArctisHeadset.EqualizerBytes(ArctisHeadset.Presets["bass"]);
        Assert.Equal(new byte[] { 0x1f, 0x20, 0x1a, 0x15, 0x15, 0x16, 0x16, 0x16, 0x16, 0x23, 0x00 }, bytes);
        Assert.All(ArctisHeadset.EqualizerBytes(ArctisHeadset.Presets["flat"]).Take(10), b => Assert.Equal(0x18, b));
        Assert.Equal(0x00, ArctisHeadset.EqualizerBytes([-20, 20, 0, 0, 0, 0, 0, 0, 0, 0])[0]); // clamped to -12 dB
        Assert.Throws<ArgumentException>(() => ArctisHeadset.EqualizerBytes([0, 0]));
    }

    [Fact]
    public void Arctis_commands_are_padded_output_reports()
    {
        var d = new HidInfo("x", 0x1038, 0x220E, 0xFFC0, 1, 65, 65, 515);
        var cmd = ArctisHeadset.Command(d, 0x39, 2); // sidetone medium
        Assert.Equal(65, cmd.Length);
        Assert.Equal(new byte[] { 0x00, 0x39, 0x02, 0x00 }, cmd[..4]);
    }

    [Fact]
    public void DualSense_lights_touch_only_the_light_fields()
    {
        var r = DualSense.LightsReport(new DualSense.Lights(0, 60, 255, Player: 1, MicLed: false), 64);
        Assert.Equal(64, r.Length);
        Assert.Equal(0x02, r[0]);       // USB output report
        Assert.Equal(0x00, r[1]);       // valid_flag0: no rumble, no audio
        Assert.Equal(0x15, r[2]);       // valid_flag1: mic light, lightbar, player lights
        Assert.Equal(0x00, r[3]);       // motors untouched
        Assert.Equal(0x00, r[4]);
        Assert.Equal(0x24, r[44]);      // player 1: the middle light, without fading
        Assert.Equal(new byte[] { 0, 60, 255 }, r[45..48]);
    }

    [Fact]
    public void Apex_direct_report_colours_every_key_and_the_accent()
    {
        var static_ = new KeyboardLighting { Enabled = true, Main = "#FF6A1A", Accent = "#FFFFFF" };
        var r = ApexKeyboard.DirectReport(KeyboardEffects.Frame(static_, 0));
        Assert.Equal(643, r.Length);
        Assert.Equal(0x3A, r[1]);
        Assert.Equal(ApexKeyboard.Keys.Length, r[2]);
        int At(byte key) => Array.IndexOf(ApexKeyboard.Keys, key);
        var a = At(0x04); // A: an accent key
        Assert.Equal(new byte[] { 0x04, 255, 255, 255 }, r[(3 + a * 4)..(7 + a * 4)]);
        var b = At(0x05); // B: a normal key
        Assert.Equal(new byte[] { 0x05, 255, 106, 26 }, r[(3 + b * 4)..(7 + b * 4)]);
    }

    [Fact]
    public void Brightness_scales_every_key()
    {
        var half = KeyboardEffects.Frame(new KeyboardLighting { Enabled = true, Main = "#FF6A1A", Brightness = 50 }, 0);
        Assert.Equal(new Rgb(128, 53, 13), half[0x05]);
        var off = KeyboardEffects.Frame(new KeyboardLighting { Enabled = true, Main = "#FFFFFF", Brightness = 0 }, 0);
        Assert.All(off.Values, c => Assert.Equal(default, c));
    }

    [Fact]
    public void Gradient_runs_from_the_left_edge_to_the_right()
    {
        var f = KeyboardEffects.Frame(new KeyboardLighting { Enabled = true, Effect = "gradient", Main = "#FF0000", Second = "#0000FF" }, 0);
        Assert.True(f[0x29].R > 230 && f[0x29].B < 25);  // Esc, far left: nearly all red
        Assert.True(f[0x56].B > 230 && f[0x56].R < 25);  // keypad minus, far right: nearly all blue
    }

    [Fact]
    public void Only_moving_effects_need_frames()
    {
        Assert.True(KeyboardEffects.Animated(new KeyboardLighting { Enabled = true, Effect = "wave" }));
        Assert.True(KeyboardEffects.Animated(new KeyboardLighting { Enabled = true, Effect = "music" }));
        Assert.False(KeyboardEffects.Animated(new KeyboardLighting { Enabled = true, Effect = "gradient" }));
        Assert.False(KeyboardEffects.Animated(new KeyboardLighting { Enabled = false, Effect = "wave" }));
    }

    [Fact]
    public void Music_effect_follows_the_sound_level()
    {
        var l = new KeyboardLighting { Enabled = true, Effect = "music", Main = "#FFFFFF" };
        Assert.True(KeyboardEffects.Frame(l, 0, 0)[0x05].R < 30);    // silence: dim
        Assert.Equal(255, KeyboardEffects.Frame(l, 0, 1)[0x05].R);   // a beat: full
    }

    [Fact]
    public void The_layout_covers_every_key_once_and_fits_its_frame()
    {
        var codes = KeyboardLayout.Keys.Select(k => k.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(104, codes.Count); // full-size ANSI
        Assert.All(KeyboardLayout.Keys, k => Assert.True(k.X + k.W <= KeyboardLayout.Width && k.Y + k.H <= KeyboardLayout.Height));
        Assert.All(KeyboardLayout.Accent, code => Assert.Contains(code, codes));
    }

    [Fact]
    public void Screen_report_is_the_0x61_picture()
    {
        var bits = new byte[640];
        bits[0] = 0x80; // the top-left pixel
        var r = ApexKeyboard.ScreenReport(bits);
        Assert.Equal(643, r.Length);
        Assert.Equal(0x00, r[0]);
        Assert.Equal(0x61, r[1]);
        Assert.Equal(0x80, r[2]);
        Assert.Throws<ArgumentException>(() => ApexKeyboard.ScreenReport(new byte[10]));
    }

    [Fact]
    public void Screen_pictures_pack_row_by_row_high_bit_first()
    {
        using var bmp = new System.Drawing.Bitmap(128, 40);
        bmp.SetPixel(0, 0, System.Drawing.Color.White);
        bmp.SetPixel(9, 1, System.Drawing.Color.White);
        var bits = OledScreen.Pack(bmp);
        Assert.Equal(640, bits.Length);
        Assert.Equal(0x80, bits[0]);
        Assert.Equal(0x40, bits[16 + 1]); // row 1, byte 1, second pixel
        var shifted = OledScreen.Pack(bmp, 1, 0);
        Assert.Equal(0x40, shifted[0]); // moved one pixel right
    }

    [Fact]
    public void Every_screen_mode_draws_something()
    {
        Assert.Contains(OledScreen.Clock(new DateTime(2026, 10, 27, 19, 42, 30), english: true), b => b != 0);
        Assert.Contains(OledScreen.Text("שלום", (0, 0)), b => b != 0);
        Assert.Contains(OledScreen.Devices([], (0, 0)), b => b != 0); // icons and dashes even with nothing connected
        foreach (var mode in new[] { "stars", "matrix", "energy", "sound", "goal" })
        {
            var a = ScreenAnimation.For(mode)!;
            for (var t = 0.0; t < 3; t += 0.05) Assert.Equal(640, a.Frame(t, 0.5f).Length);
        }
    }

    [Fact]
    public void Generic_bluetooth_names_are_translated()
    {
        I18n.English = false;
        Assert.Equal("אוזניות", AudioNames.Describe("Headphones", "OnePlus Buds 4", true).Sub);
        I18n.English = true;
        Assert.Equal("Headphones", AudioNames.Describe("Headphones", "OnePlus Buds 4", true).Sub);
    }
}
