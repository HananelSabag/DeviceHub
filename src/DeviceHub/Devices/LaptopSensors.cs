using System.Management;
using System.Runtime.InteropServices;
using DeviceHub.Interop;
using Microsoft.Win32;

namespace DeviceHub.Devices;

/// <summary>
/// The laptop itself. Temperatures and fans come from InsydeDCHU.dll, the vendor's own DLL that Control Center 3.0
/// uses (installed with its ACPI bridge driver); only GetDCHU_Data_Buffer is called, with the read command 12.
/// Load comes from Windows' performance counters (as in Task Manager), memory and battery from Windows itself.
/// The one thing changed here is Windows' power mode. Fan control stays in Control Center: nothing writes fan or EC settings.
/// </summary>
internal static unsafe class LaptopSensors
{
    private const int FanStatusCommand = 12;
    private static readonly Lock gate = new();
    private static delegate* unmanaged[Stdcall]<int, byte*, int> getData;
    private static bool loaded;
    private static string? loadError;
    private static string? model;
    private static Pdh? pdh;
    private static long nextBatteryLook;
    private static double? watts;

    public static string Model => model ??= ReadModel();

    public static DeviceReading Read()
    {
        lock (gate)
        {
            if (!loaded) Load();
            var ec = (Fans?)null;
            if (getData != null)
            {
                var buf = new byte[256];
                fixed (byte* p = buf) getData(FanStatusCommand, p);
                ec = Parse(buf);
            }
            var load = (pdh ??= new Pdh()).Read();
            var (usedGb, totalGb) = Memory();
            var power = Power();
            if (ec is null && load.CpuPercent is null && power.Percent is null)
                return new() { Id = "laptop", Model = Model, State = DeviceState.Missing, Detail = loadError };
            return new()
            {
                Id = "laptop", Model = Model, State = DeviceState.Ready,
                Temperature = ec?.CpuTemp, GpuTemperature = ec?.GpuTemp,
                FanDuty = ec?.CpuDuty, Fan2Duty = ec?.GpuDuty, FanRpm = ec?.CpuRpm, Fan2Rpm = ec?.GpuRpm,
                CpuLoad = Round(load.CpuPercent is { } c ? Math.Min(100, c) : null), CpuSpeed = Round(load.CpuPerformance), GpuLoad = Round(load.GpuPercent),
                MemoryUsed = usedGb, MemoryTotal = totalGb,
                LaptopBattery = power.Percent, OnCharger = power.OnCharger, Charging = power.Charging, Watts = watts,
                PowerMode = PowerMode(),
                Detail = ec is null ? loadError : null,
            };
        }
    }

    private static int? Round(double? v) => v is null ? null : (int)Math.Round(v.Value);

    internal sealed record Fans(int? CpuTemp, int? GpuTemp, int CpuDuty, int GpuDuty, int? CpuRpm, int? GpuRpm);

    /// <summary>
    /// Command 12's buffer on this Clevo-built GIGABYTE: fan 1 (CPU) tacho period at bytes 2-3 and fan 2 (GPU) at 4-5
    /// (rpm = 2156220 / period), CPU fan duty at 16, CPU temperature at 18, GPU fan duty at 19, GPU temperature at 21.
    /// Checked against nvidia-smi and the fans' sound.
    /// </summary>
    internal static Fans? Parse(byte[] buf)
    {
        if (buf.Length < 22) return null;
        int? Temp(byte b) => b is > 0 and < 120 ? b : null;
        int? Rpm(int hi, int lo) { var period = (buf[hi] << 8) | buf[lo]; return period is 0 or >= 0xFF00 ? 0 : (int)Math.Round(2156220.0 / period); }
        int Duty(byte b) => (int)Math.Round(b * 100 / 255.0);
        return new Fans(Temp(buf[18]), Temp(buf[21]), Duty(buf[16]), Duty(buf[19]), Rpm(2, 3), Rpm(4, 5));
    }

    private static (double? Used, double? Total) Memory()
    {
        var m = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref m)) return (null, null);
        const double Gb = 1024.0 * 1024 * 1024;
        return (Math.Round((m.TotalPhys - m.AvailPhys) / Gb, 1), Math.Round(m.TotalPhys / Gb, 1));
    }

    private static (int? Percent, bool OnCharger, bool Charging) Power()
    {
        if (!GetSystemPowerStatus(out var s) || s.BatteryFlag == 128) return (null, s.ACLineStatus == 1, false); // 128: no battery
        // How much the laptop draws, from the battery's own rate: every 15 seconds (it is a WMI question).
        if (Environment.TickCount64 >= nextBatteryLook)
        {
            nextBatteryLook = Environment.TickCount64 + 15_000;
            watts = BatteryWatts();
        }
        return (s.BatteryLifePercent <= 100 ? s.BatteryLifePercent : null, s.ACLineStatus == 1, (s.BatteryFlag & 8) != 0);
    }

    private static double? BatteryWatts()
    {
        try
        {
            using var search = new ManagementObjectSearcher(@"root\wmi", "SELECT DischargeRate, ChargeRate FROM BatteryStatus");
            foreach (ManagementObject o in search.Get())
            {
                var discharge = Convert.ToInt64(o["DischargeRate"] ?? 0);
                var charge = Convert.ToInt64(o["ChargeRate"] ?? 0);
                var mw = discharge > 0 ? -discharge : charge;
                return mw == 0 ? 0 : Math.Round(mw / 1000.0, 1); // + charging, - on battery
            }
        }
        catch (ManagementException) { }
        return null;
    }

    // ---- Windows power mode (Settings → Power → Power mode) ----

    private static readonly Guid BestEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid BestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");

    /// <summary>efficiency | balanced | performance</summary>
    public static string? PowerMode()
    {
        if (PowerGetEffectiveOverlayScheme(out var g) != 0) return null;
        return g == BestEfficiency ? "efficiency" : g == BestPerformance ? "performance" : "balanced";
    }

    public static void SetPowerMode(string mode)
    {
        var g = mode switch { "efficiency" => BestEfficiency, "performance" => BestPerformance, "balanced" => Guid.Empty, _ => throw new ArgumentException(mode) };
        if (PowerSetActiveOverlayScheme(g) != 0) throw new IOException("Windows kept its power mode");
        if (PowerMode() != mode) throw new IOException("Windows kept its power mode");
    }

    // ---- The vendor DLL ----

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
            return quick ?? Directory.EnumerateFiles(repo, "InsydeDCHU.dll", new System.IO.EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).FirstOrDefault();
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

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx { public uint Length, MemoryLoad; public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    private const uint LoadWithAlteredSearchPath = 0x00000008;
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)] private static extern IntPtr GetProcAddress(IntPtr lib, string name);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx m);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SystemPowerStatus s);
    [DllImport("powrprof.dll")] private static extern uint PowerGetEffectiveOverlayScheme(out Guid scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveOverlayScheme(Guid scheme);
}
