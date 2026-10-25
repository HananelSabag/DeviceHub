using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;
using DeviceHub.Interop;

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
        var r = ApexKeyboard.DirectReport(new(255, 106, 26), new(255, 255, 255));
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
    public void Apex_brightness_scales_the_colours()
    {
        Assert.Equal(new ApexKeyboard.Rgb(128, 53, 13), ApexKeyboard.Scale(new(255, 106, 26), 50));
        Assert.Equal(new ApexKeyboard.Rgb(0, 0, 0), ApexKeyboard.Scale(new(255, 255, 255), 0));
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
