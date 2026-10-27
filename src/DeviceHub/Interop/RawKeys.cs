using System.Runtime.InteropServices;

namespace DeviceHub.Interop;

/// <summary>
/// Which key on the Apex was just pressed, for the key-press lighting effects. Windows' Raw Input sends DeviceHub a
/// copy of each keyboard event (RIDEV_INPUTSINK, so also while another window has focus); nothing is intercepted,
/// blocked or stored, and only key-downs from the SteelSeries keyboard are used. Registered only while such an effect is on.
/// </summary>
internal static class RawKeys
{
    public const int WmInput = 0x00FF;
    private static readonly Dictionary<IntPtr, bool> fromApex = [];

    /// <summary>Start (or stop) receiving keyboard input on this window.</summary>
    public static bool Listen(IntPtr hwnd, bool on)
    {
        var device = new RawInputDevice { UsagePage = 0x01, Usage = 0x06, Flags = on ? InputSink : Remove, Target = on ? hwnd : IntPtr.Zero };
        return RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    /// <summary>The HID usage code of a key-down from the Apex in this WM_INPUT, or null.</summary>
    public static byte? KeyDown(IntPtr lParam)
    {
        uint size = 0;
        var header = (uint)Marshal.SizeOf<RawInputHeader>();
        GetRawInputData(lParam, RidInput, IntPtr.Zero, ref size, header);
        if (size == 0 || size > 256) return null;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RidInput, buf, ref size, header) != size) return null;
            var h = Marshal.PtrToStructure<RawInputHeader>(buf);
            if (h.Type != 1) return null; // keyboard
            var k = Marshal.PtrToStructure<RawKeyboard>(buf + (int)header);
            if ((k.Flags & 1) != 0) return null; // a key going up
            if (!IsApex(h.Device)) return null;  // the laptop's own keyboard doesn't light the Apex
            return HidCode(k.VKey, k.MakeCode, (k.Flags & 2) != 0);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static bool IsApex(IntPtr device)
    {
        if (device == IntPtr.Zero) return false; // injected input (on-screen keyboards, macros)
        if (fromApex.TryGetValue(device, out var known)) return known;
        uint chars = 0;
        GetRawInputDeviceInfo(device, RidiDeviceName, IntPtr.Zero, ref chars);
        var name = "";
        if (chars is > 0 and < 1024)
        {
            var buf = Marshal.AllocHGlobal((int)chars * 2);
            try { if (GetRawInputDeviceInfo(device, RidiDeviceName, buf, ref chars) > 0) name = Marshal.PtrToStringUni(buf) ?? ""; }
            finally { Marshal.FreeHGlobal(buf); }
        }
        return fromApex[device] = name.Contains("VID_1038&PID_161C", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A Windows virtual key → the key's HID usage code (what the Apex's colour table uses). The e0 flag tells the
    /// navigation block from the number pad, and the scan code tells left Shift from right.
    /// </summary>
    internal static byte? HidCode(ushort vk, ushort scan, bool e0)
    {
        if (vk is >= 0x41 and <= 0x5A) return (byte)(0x04 + vk - 0x41);           // A-Z
        if (vk is >= 0x31 and <= 0x39) return (byte)(0x1E + vk - 0x31);           // 1-9
        if (vk is >= 0x70 and <= 0x7B) return (byte)(0x3A + vk - 0x70);           // F1-F12
        if (vk is >= 0x60 and <= 0x69) return Numpad[vk - 0x60];                  // numpad digits (Num Lock on)
        return vk switch
        {
            0x30 => 0x27, 0x0D => e0 ? (byte)0x58 : (byte)0x28, 0x1B => 0x29, 0x08 => 0x2A, 0x09 => 0x2B, 0x20 => 0x2C,
            0xBD => 0x2D, 0xBB => 0x2E, 0xDB => 0x2F, 0xDD => 0x30, 0xDC => 0x31, 0xBA => 0x33, 0xDE => 0x34, 0xC0 => 0x35,
            0xBC => 0x36, 0xBE => 0x37, 0xBF => 0x38, 0x14 => 0x39, 0x2C => 0x46, 0x91 => 0x47, 0x13 => 0x48,
            // The navigation keys: without e0 they are the number pad with Num Lock off.
            0x2D => e0 ? (byte)0x49 : (byte)0x62, 0x24 => e0 ? (byte)0x4A : (byte)0x5F, 0x21 => e0 ? (byte)0x4B : (byte)0x61,
            0x2E => e0 ? (byte)0x4C : (byte)0x63, 0x23 => e0 ? (byte)0x4D : (byte)0x59, 0x22 => e0 ? (byte)0x4E : (byte)0x5B,
            0x27 => e0 ? (byte)0x4F : (byte)0x5E, 0x25 => e0 ? (byte)0x50 : (byte)0x5C, 0x28 => e0 ? (byte)0x51 : (byte)0x5A,
            0x26 => e0 ? (byte)0x52 : (byte)0x60, 0x0C => 0x5D,
            0x90 => 0x53, 0x6F => 0x54, 0x6A => 0x55, 0x6D => 0x56, 0x6B => 0x57, 0x6E => 0x63,
            0x10 => scan == 0x36 ? (byte)0xE5 : (byte)0xE1,                         // Shift: right has scan code 0x36
            0x11 => e0 ? (byte)0xE4 : (byte)0xE0, 0x12 => e0 ? (byte)0xE6 : (byte)0xE2,
            0x5B => 0xE3, 0x5C => 0xE7, 0x5D => 0x65,
            _ => null,
        };
    }

    private static readonly byte[] Numpad = [0x62, 0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61];

    private const uint InputSink = 0x00000100, Remove = 0x00000001, RidInput = 0x10000003, RidiDeviceName = 0x20000007;

    [StructLayout(LayoutKind.Sequential)] private struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawInputHeader { public uint Type, Size; public IntPtr Device, WParam; }
    [StructLayout(LayoutKind.Sequential)] private struct RawKeyboard { public ushort MakeCode, Flags, Reserved, VKey; public uint Message, ExtraInformation; }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
}
