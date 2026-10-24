using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeviceHub.Devices;

/// <summary>
/// Temperature and fan duty of a Clevo-built laptop, read through InsydeDCHU.dll: the vendor's own DLL that
/// Control Center 3.0 uses, installed with its ACPI bridge driver. Only GetDCHU_Data_Buffer is called, with the
/// read command 12. Fan control stays in Control Center; nothing here writes fan or EC settings.
/// </summary>
internal static unsafe class LaptopSensors
{
    private const int FanStatusCommand = 12;
    private static readonly Lock gate = new();
    private static delegate* unmanaged[Stdcall]<int, byte*, int> getData;
    private static bool loaded;
    private static string? loadError;
    private static string? model;

    public static string Model => model ??= ReadModel();

    public static DeviceReading Read()
    {
        lock (gate)
        {
            if (!loaded) Load();
            if (getData == null) return new() { Id = "laptop", Model = Model, State = DeviceState.Missing, Detail = loadError };
            var buf = new byte[256];
            fixed (byte* p = buf) getData(FanStatusCommand, p);
            var (temp, duty) = Parse(buf);
            return temp is null
                ? new() { Id = "laptop", Model = Model, State = DeviceState.Error, Detail = "no temperature in the answer" }
                : new() { Id = "laptop", Model = Model, State = DeviceState.Ready, Temperature = temp, FanDuty = duty };
        }
    }

    /// <summary>Command 12's buffer: fan duty at byte 16 (0-255), temperature at byte 18 (°C).</summary>
    internal static (int? Temperature, int? FanDuty) Parse(byte[] buf)
    {
        if (buf.Length < 19) return (null, null);
        int? temp = buf[18] is > 0 and < 120 ? buf[18] : null;
        return (temp, (int)Math.Round(buf[16] * 100 / 255.0));
    }

    private static void Load()
    {
        loaded = true;
        if (!Environment.Is64BitProcess) { loadError = "needs a 64-bit process"; return; }
        var dll = FindDll();
        if (dll is null) { loadError = "InsydeDCHU.dll not found (not a Clevo laptop?)"; return; }
        var lib = LoadLibraryEx(dll, IntPtr.Zero, LoadWithAlteredSearchPath);
        if (lib == IntPtr.Zero) { loadError = $"cannot load {dll} ({Marshal.GetLastWin32Error()})"; return; }
        var fn = GetProcAddress(lib, "GetDCHU_Data_Buffer");
        if (fn == IntPtr.Zero) { loadError = "GetDCHU_Data_Buffer is not exported"; return; }
        getData = (delegate* unmanaged[Stdcall]<int, byte*, int>)fn;
    }

    /// <summary>The DLL lives in the driver store, in the ACPI bridge package's folder.</summary>
    private static string? FindDll()
    {
        var repo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "DriverStore", "FileRepository");
        try
        {
            var quick = Directory.EnumerateDirectories(repo, "acpibridge*")
                .Select(dir => Path.Combine(dir, "InsydeDCHU.dll"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            return quick ?? Directory.EnumerateFiles(repo, "InsydeDCHU.dll", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string ReadModel()
    {
        try
        {
            using var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            var maker = (bios?.GetValue("SystemManufacturer") as string)?.Trim();
            var product = (bios?.GetValue("SystemProductName") as string)?.Trim();
            if (string.IsNullOrEmpty(product)) return "Laptop";
            return string.IsNullOrEmpty(maker) || product.StartsWith(maker, StringComparison.OrdinalIgnoreCase) ? product : $"{maker} {product}";
        }
        catch { return "Laptop"; }
    }

    private const uint LoadWithAlteredSearchPath = 0x00000008;
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)] private static extern IntPtr GetProcAddress(IntPtr lib, string name);
}
