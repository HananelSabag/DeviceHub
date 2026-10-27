using DeviceHub.Audio;
using DeviceHub.Core;
using DeviceHub.Devices;

namespace DeviceHub.Tests;

/// <summary>Every parser checked against bytes recorded from the real devices on this PC.</summary>
public class ParsingTests
{
    [Fact]
    public void Razer_query_has_the_packet_layout_and_crc()
    {
        var buf = RazerMouse.BuildQuery(0x1F, 0x07, 0x80, 0x02);
        Assert.Equal(91, buf.Length);
        Assert.Equal(0x00, buf[0]); // report id
        Assert.Equal(0x1F, buf[2]); // transaction id
        Assert.Equal(0x02, buf[6]); // data size
        Assert.Equal(0x07, buf[7]); // class
        Assert.Equal(0x80, buf[8]); // command
        Assert.Equal(0x1F ^ 0x00, 0x1F); // tid is outside the CRC range
        Assert.Equal((byte)(0x02 ^ 0x07 ^ 0x80), buf[89]);
    }

    [Fact]
    public void Razer_answers_are_read()
    {
        var packet = new byte[90];
        packet[0] = 0x02; packet[6] = 0x07; packet[7] = 0x80; packet[9] = 242; // battery as the mouse sent it
        Assert.Equal(95, RazerMouse.BatteryPercent(packet));
        packet[8] = 0x01;
        Assert.Equal(1000, RazerMouse.PollingHz(packet));
        packet[8] = 0x08;
        Assert.Equal(125, RazerMouse.PollingHz(packet));
        Assert.Equal(RazerMouse.Status.Timeout, RazerMouse.ParseStatus(0x04)); // the mouse is asleep
        Assert.Equal(RazerMouse.Status.Ok, RazerMouse.ParseStatus(0x02));
    }

    [Fact]
    public void Arctis_status_with_the_headset_off()
    {
        var res = new byte[65];
        new byte[] { 0x00, 0xB0, 0x01, 0x00, 0x00, 0xFF, 0xFF }.CopyTo(res, 0); // recorded with the headset off
        var s = ArctisHeadset.Parse(res);
        Assert.NotNull(s);
        Assert.False(s.Online);
    }

    [Fact]
    public void Arctis_status_with_the_headset_on()
    {
        var res = new byte[65];
        new byte[] { 0x00, 0xB0, 0x03, 0x03, 0x00, 92, 100 }.CopyTo(res, 0);
        var s = ArctisHeadset.Parse(res)!;
        Assert.True(s.Online);
        Assert.Equal(75, s.Battery);
        Assert.False(s.Charging);
        Assert.Equal(92, s.Game);
        Assert.Equal(100, s.Chat);
    }

    [Fact]
    public void Arctis_ignores_other_reports()
    {
        var res = new byte[65];
        res[1] = 0x45; // something the dongle sent by itself
        Assert.Null(ArctisHeadset.Parse(res));
    }

    [Theory]
    [InlineData(0x2A, 100, false, true)]  // full
    [InlineData(0x15, 55, true, false)]   // charging, level 5
    [InlineData(0x03, 35, false, false)]  // on battery, level 3
    [InlineData(0x0A, 100, false, false)] // level 10 caps at 100
    public void DualSense_battery_from_a_usb_report(byte status, int battery, bool charging, bool full)
    {
        var r = new byte[64];
        r[0] = 0x01;
        r[53] = status;
        var s = DualSense.Parse(r)!;
        Assert.Equal(battery, s.Battery);
        Assert.Equal(charging, s.Charging);
        Assert.Equal(full, s.Full);
    }

    [Fact]
    public void DualSense_bluetooth_reports()
    {
        var full = new byte[78];
        full[0] = 0x31;
        full[54] = 0x07;
        Assert.Equal(75, DualSense.Parse(full)!.Battery);
        var simple = new byte[10];
        simple[0] = 0x01;
        Assert.Null(DualSense.Parse(simple)); // the short Bluetooth report has no battery
    }

    [Fact]
    public void Laptop_sensors_from_command_12()
    {
        // Recorded: 00-00-02-28-02-D6-00-00-F9-40-00-00-01-3D-F9-FF-A5-37-41-82-...
        var buf = new byte[256];
        new byte[] { 0x00, 0x00, 0x02, 0x28, 0x02, 0xD6, 0x00, 0x00, 0xF9, 0x40, 0x00, 0x00, 0x01, 0x3D, 0xF9, 0xFF, 0xA5, 0x37, 0x41, 0x82 }.CopyTo(buf, 0);
        var f = LaptopSensors.Parse(buf)!;
        Assert.Equal(65, f.CpuTemp);
        Assert.Equal(65, f.CpuDuty);
        Assert.Null(LaptopSensors.Parse(new byte[4]));
    }

    [Theory]
    [InlineData("Headphones", "4- Arctis 7+", true, "Arctis 7+", "headset")]
    [InlineData("Microphone", "4- Arctis 7+", false, "Arctis 7+", "headset")]
    [InlineData("Headphones", "UGREEN-90748", true, "Logitech speakers", "speakers")]
    [InlineData("VG27AQ1A", "NVIDIA High Definition Audio", true, "VG27AQ1A", "monitor")]
    [InlineData("Speakers", "2- DualSense Edge Wireless Controller", true, "DualSense Edge", "controller")]
    [InlineData("Speakers", "3- High Definition Audio Device", true, "Laptop speakers", "laptop")]
    [InlineData("Microphone", "3- High Definition Audio Device", false, "Laptop mic", "laptop")]
    [InlineData("Headphones", "OnePlus Buds 4", true, "OnePlus Buds 4", "earbuds")]
    public void Audio_names_are_the_real_devices(string desc, string adapter, bool output, string name, string kind)
    {
        I18n.English = true;
        var look = AudioNames.Describe(desc, adapter, output);
        Assert.Equal(name, look.Name);
        Assert.Equal(kind, look.Kind);
        Assert.False(look.HandsFree);
    }

    [Fact]
    public void Bluetooth_call_profiles_are_marked()
    {
        I18n.English = true;
        Assert.True(AudioNames.Describe("Headset", "UGREEN-90748 Hands-Free", true).HandsFree);
        Assert.True(AudioNames.Describe("Headset", "UGREEN-90748", false).HandsFree); // the receiver has no real microphone
        Assert.True(AudioNames.Describe("Headset", "OnePlus Buds 4 Hands-Free", true).HandsFree);
    }

    [Fact]
    public void Old_endpoints_collapse_and_call_profiles_hide()
    {
        AudioEndpoint E(string id, string name, string kind, bool active, bool def = false, bool calls = false) =>
            new() { Id = id, Flow = "output", Name = name, Kind = kind, Active = active, Default = def, Calls = calls, Windows = id };
        var all = new[]
        {
            E("a4", "Arctis 7+", "headset", true),
            E("a2", "Arctis 7+", "headset", false),   // the same headset in an old USB port
            E("a3", "Arctis 7+", "headset", false),
            E("u", "Logitech speakers", "speakers", false),
            E("uh", "UGREEN calls", "speakers", false),
            E("mon2", "NVIDIA Output", "monitor", false), // an empty display port
            E("buds", "OnePlus Buds 4", "earbuds", false),
            E("lap", "Laptop speakers", "laptop", true, def: true),
        };
        var list = AudioDevices.Tidy(all, new HashSet<string> { "uh" });
        Assert.Equal(["a4", "u", "lap"], list.Select(e => e.Id));
    }

    [Fact]
    public void A_call_profile_in_use_stays_visible()
    {
        var e = new AudioEndpoint { Id = "uh", Flow = "input", Name = "UGREEN calls", Kind = "speakers", Active = true, Calls = true, Windows = "uh" };
        Assert.Single(AudioDevices.Tidy([e], new HashSet<string> { "uh" }));
    }

    [Fact]
    public void Settings_survive_a_broken_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"devicehub-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        Assert.Equal("he", AppSettings.Load(path).Language);
        new AppSettings { Language = "en", SwitchCalls = false }.Save(path);
        var back = AppSettings.Load(path);
        Assert.Equal("en", back.Language);
        Assert.False(back.SwitchCalls);
        File.Delete(path);
    }
}
