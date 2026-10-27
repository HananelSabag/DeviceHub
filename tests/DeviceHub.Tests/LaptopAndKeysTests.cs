using DeviceHub.Devices;
using DeviceHub.Interop;
using DeviceHub.Lighting;

namespace DeviceHub.Tests;

/// <summary>The laptop's sensor buffer, the key map for key-press effects, and the screen's transitions.</summary>
public class LaptopAndKeysTests
{
    [Fact]
    public void Laptop_buffer_from_this_machine()
    {
        // Recorded with nvidia-smi showing the GPU at 57 °C:
        // 00-00-01-E9-02-2E-00-00-56-40-00-00-01-3A-F9-FF-BF-38-46-BF-00-39-00-01
        var buf = new byte[256];
        new byte[] { 0x00, 0x00, 0x01, 0xE9, 0x02, 0x2E, 0x00, 0x00, 0x56, 0x40, 0x00, 0x00, 0x01, 0x3A, 0xF9, 0xFF, 0xBF, 0x38, 0x46, 0xBF, 0x00, 0x39, 0x00, 0x01 }.CopyTo(buf, 0);
        var f = LaptopSensors.Parse(buf)!;
        Assert.Equal(70, f.CpuTemp);
        Assert.Equal(57, f.GpuTemp);
        Assert.Equal(75, f.CpuDuty);
        Assert.Equal(75, f.GpuDuty);
        Assert.Equal(4409, f.CpuRpm);  // 2156220 / 0x01E9
        Assert.Equal(3864, f.GpuRpm);  // 2156220 / 0x022E
    }

    [Fact]
    public void A_stopped_fan_reads_zero()
    {
        var buf = new byte[256];
        buf[2] = 0xFF; buf[3] = 0xFF; buf[18] = 45;
        var f = LaptopSensors.Parse(buf)!;
        Assert.Equal(0, f.CpuRpm);
        Assert.Equal(0, f.GpuRpm);
        Assert.Null(f.GpuTemp); // 0 is no reading
    }

    [Theory]
    [InlineData(0x57, 0x11, false, 0x1A)]  // W
    [InlineData(0x41, 0x1E, false, 0x04)]  // A
    [InlineData(0x31, 0x02, false, 0x1E)]  // 1
    [InlineData(0x30, 0x0B, false, 0x27)]  // 0
    [InlineData(0x26, 0x48, true, 0x52)]   // Up, from the arrow keys
    [InlineData(0x26, 0x48, false, 0x60)]  // the same key code from the number pad (8, Num Lock off)
    [InlineData(0x0D, 0x1C, true, 0x58)]   // number pad Enter
    [InlineData(0x0D, 0x1C, false, 0x28)]  // Enter
    [InlineData(0x10, 0x2A, false, 0xE1)]  // left Shift
    [InlineData(0x10, 0x36, false, 0xE5)]  // right Shift
    [InlineData(0x11, 0x1D, true, 0xE4)]   // right Ctrl
    [InlineData(0x70, 0x3B, false, 0x3A)]  // F1
    [InlineData(0x65, 0x4D, false, 0x5D)]  // number pad 5
    public void Windows_keys_map_to_the_keyboards_key_codes(ushort vk, ushort scan, bool e0, byte hid)
    {
        Assert.Equal(hid, RawKeys.HidCode(vk, scan, e0));
    }

    [Fact]
    public void Every_mapped_key_is_on_the_layout()
    {
        var codes = KeyboardLayout.Keys.Select(k => k.Code).ToHashSet();
        var missing = new SortedSet<byte>();
        for (ushort vk = 0; vk < 256; vk++)
            foreach (var e0 in new[] { false, true })
                if (RawKeys.HidCode(vk, 0x2A, e0) is { } code && !codes.Contains(code)) missing.Add(code);
        // Only right Win: the Apex has its SteelSeries key there, so that press simply lights nothing.
        Assert.Equal(new byte[] { 0xE7 }, missing);
    }

    [Fact]
    public void A_pressed_key_lights_and_fades()
    {
        var l = new KeyboardLighting { Enabled = true, Effect = "reactive", Main = "#FFFFFF", Second = "#000000", Speed = 5 };
        var press = new[] { new KeyboardEffects.Press(0x04, 10.0) };
        Assert.Equal(255, KeyboardEffects.Frame(l, 10.0, 0, press)[0x04].R);       // A, the moment it is pressed
        Assert.Equal(0, KeyboardEffects.Frame(l, 10.0, 0, press)[0x05].R);         // B stays dark
        Assert.Equal(0, KeyboardEffects.Frame(l, 10.0 + KeyboardEffects.Fade(l), 0, press)[0x04].R); // faded out
    }

    [Fact]
    public void A_ripple_reaches_further_keys_later()
    {
        var l = new KeyboardLighting { Enabled = true, Effect = "ripple", Main = "#FFFFFF", Second = "#000000", Speed = 5 };
        var press = new[] { new KeyboardEffects.Press(0x04, 0) };        // A
        var near = KeyboardEffects.Frame(l, 0.07, 0, press)[0x16].R;      // S, next door
        var far = KeyboardEffects.Frame(l, 0.07, 0, press)[0x0F].R;       // L, far away
        Assert.True(near > far);
        var farLater = KeyboardEffects.Frame(l, 0.5, 0, press)[0x0F].R;
        Assert.True(farLater > far);
    }

    [Fact]
    public void Transitions_start_on_one_picture_and_end_on_the_next()
    {
        var a = Enumerable.Repeat((byte)0xFF, 640).ToArray();
        var b = new byte[640];
        for (var kind = 0; kind < 5; kind++)
        {
            Assert.Equal(a, ScreenAnimation.Show.Transition(a, b, 0, kind));
            Assert.Equal(b, ScreenAnimation.Show.Transition(a, b, 1.0001, kind));
            var half = ScreenAnimation.Show.Transition(a, b, 0.5, kind);
            Assert.Contains(half, x => x != 0);
            Assert.Contains(half, x => x != 0xFF);
        }
    }

    [Fact]
    public void Show_and_power_up_play()
    {
        foreach (var mode in new[] { "show", "power" })
        {
            var anim = ScreenAnimation.For(mode)!;
            for (var t = 0.0; t < 20; t += 0.25) Assert.Equal(640, anim.Frame(t, 0.3f).Length);
        }
        Assert.Contains(OledScreen.Workload(new DeviceReading { Id = "laptop", Model = "x", State = DeviceState.Ready, Temperature = 70, CpuLoad = 40 }, (0, 0)), b => b != 0);
    }
}
