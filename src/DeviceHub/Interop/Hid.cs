using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace DeviceHub.Interop;

/// <summary>One HID collection: its path, who made it, and how long its reports are.</summary>
internal sealed record HidInfo(string Path, ushort Vid, ushort Pid, ushort UsagePage, ushort Usage, int InputLength, int OutputLength, int FeatureLength)
{
    /// <summary>"mi_03" or "mi_00&amp;col02": which interface of the USB device this collection is.</summary>
    public string Interface => Regex.Match(Path, @"mi_\d\d(&col\d\d)?", RegexOptions.IgnoreCase).Value.ToLowerInvariant();

    public bool Bluetooth => Path.Contains("{00001124-0000-1000-8000-00805f9b34fb}", StringComparison.OrdinalIgnoreCase)
        || Path.Contains("{00001812-0000-1000-8000-00805f9b34fb}", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// User-mode HID through Windows' own hid.dll: list collections, read input reports, exchange feature reports.
/// No driver is installed and nothing here needs admin rights.
/// </summary>
internal static class Hid
{
    /// <summary>The HID collections of the given vendors. Other devices are skipped by their path, never opened.</summary>
    public static List<HidInfo> Enumerate(params ushort[] vendors)
    {
        HidD_GetHidGuid(out var guid);
        var set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 0x12); // DIGCF_PRESENT | DIGCF_DEVICEINTERFACE
        var list = new List<HidInfo>();
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return list;
        var wanted = vendors.Select(v => v.ToString("x4")).ToArray();
        try
        {
            for (var i = 0; ; i++)
            {
                var data = new DevInterfaceData { Size = Marshal.SizeOf<DevInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data)) break;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out var size, IntPtr.Zero);
                if (size <= 0) continue;
                var detail = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6); // SP_DEVICE_INTERFACE_DETAIL_DATA_W.cbSize
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, size, out _, IntPtr.Zero)) continue;
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    // USB paths say "vid_1532", Bluetooth ones "vid&0002054c": the vendor's hex digits are in both.
                    if (wanted.Length > 0 && !wanted.Any(v => path.Contains(v, StringComparison.OrdinalIgnoreCase))) continue;
                    if (Describe(path) is { } info && (wanted.Length == 0 || vendors.Contains(info.Vid))) list.Add(info);
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
        return list;
    }

    private static HidInfo? Describe(string path)
    {
        using var h = Open(path, readWrite: false);
        if (h is null) return null;
        var a = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        if (!HidD_GetAttributes(h, ref a) || !HidD_GetPreparsedData(h, out var pre)) return null;
        try
        {
            HidP_GetCaps(pre, out var caps);
            return new HidInfo(path, a.Vid, a.Pid, caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength);
        }
        finally { HidD_FreePreparsedData(pre); }
    }

    /// <summary>
    /// A synchronous handle for feature reports. Windows keeps mice and keyboards for itself, so when read/write
    /// access is refused this falls back to a handle without access, which still carries feature reports.
    /// </summary>
    public static SafeFileHandle? Open(string path, bool readWrite)
    {
        var h = CreateFile(path, readWrite ? GenericRead | GenericWrite : 0, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (h.IsInvalid) { h.Dispose(); return null; }
        return h;
    }

    /// <summary>An overlapped handle for input (and output) reports, so a read can give up after a timeout.</summary>
    public static FileStream? OpenStream(string path, bool write)
    {
        var h = CreateFile(path, write ? GenericRead | GenericWrite : GenericRead, ShareReadWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
        if (h.IsInvalid) { h.Dispose(); return null; }
        return new FileStream(h, write ? FileAccess.ReadWrite : FileAccess.Read, 0, isAsync: true);
    }

    /// <summary>One input report (the report id first, as Windows returns it), or null if none came in time.</summary>
    public static async Task<byte[]?> ReadAsync(FileStream fs, int length, int timeoutMs)
    {
        var buf = new byte[length];
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var n = await fs.ReadAsync(buf.AsMemory(0, length), cts.Token);
            return n > 0 ? buf : null;
        }
        catch (OperationCanceledException) { return null; }
        catch (IOException) { return null; } // unplugged while reading
    }

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetAttributes(SafeFileHandle h, ref HiddAttributes a);
    [DllImport("hid.dll", SetLastError = true)] private static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, out HidpCaps caps);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] buf, int len);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetFeature(SafeFileHandle h, byte[] buf, int len);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr enumerator, IntPtr parent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int index, ref DevInterfaceData data);
    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref DevInterfaceData data, IntPtr detail, int size, out int required, IntPtr info);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);

    [StructLayout(LayoutKind.Sequential)] private struct HiddAttributes { public int Size; public ushort Vid, Pid, Version; }
    [StructLayout(LayoutKind.Sequential)] private struct HidpCaps
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices, NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DevInterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }

    private const uint GenericRead = 0x80000000, GenericWrite = 0x40000000, ShareReadWrite = 3, OpenExisting = 3, FileFlagOverlapped = 0x40000000;
}
