using System.Runtime.InteropServices;

namespace DeviceHub.Interop;

/// <summary>
/// Windows performance counters (pdh.dll), read the way Task Manager reads them: CPU utility and performance,
/// and the GPUs' 3D engine use. One query, collected on demand; the first collection only primes the rates.
/// </summary>
internal sealed class Pdh : IDisposable
{
    private readonly IntPtr query;
    private readonly IntPtr cpuUtility, cpuPerformance, gpu3d;
    private bool primed;

    public Pdh()
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out query) != 0) return;
        PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Utility", IntPtr.Zero, out cpuUtility);
        PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Performance", IntPtr.Zero, out cpuPerformance);
        // Every process's 3D engine, on every GPU: summed per GPU, like Task Manager's GPU graph.
        PdhAddEnglishCounter(query, @"\GPU Engine(*engtype_3D)\Utilization Percentage", IntPtr.Zero, out gpu3d);
    }

    public sealed record Sample(double? CpuPercent, double? CpuPerformance, double? GpuPercent);

    public Sample Read()
    {
        if (query == IntPtr.Zero || PdhCollectQueryData(query) != 0) return new(null, null, null);
        if (!primed) { primed = true; return new(null, null, null); }
        return new(Value(cpuUtility), Value(cpuPerformance), GpuBusiest());
    }

    private static double? Value(IntPtr counter)
    {
        if (counter == IntPtr.Zero) return null;
        return PdhGetFormattedCounterValue(counter, PdhFmtDouble | PdhFmtNoCap100, IntPtr.Zero, out var v) == 0 && v.CStatus == 0 ? v.DoubleValue : null;
    }

    /// <summary>The busiest GPU's 3D use: instances are named "pid_…_luid_0x…_…_engtype_3D", summed by luid.</summary>
    private double? GpuBusiest()
    {
        if (gpu3d == IntPtr.Zero) return null;
        int size = 0, count;
        if (PdhGetFormattedCounterArray(gpu3d, PdhFmtDouble, ref size, out count, IntPtr.Zero) != PdhMoreData || size == 0) return 0;
        var buf = Marshal.AllocHGlobal(size);
        try
        {
            if (PdhGetFormattedCounterArray(gpu3d, PdhFmtDouble, ref size, out count, buf) != 0) return null;
            var perGpu = new Dictionary<string, double>();
            var item = Marshal.SizeOf<PdhFmtCounterValueItem>();
            for (var i = 0; i < count; i++)
            {
                var entry = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buf + i * item);
                var name = Marshal.PtrToStringUni(entry.Name) ?? "";
                var at = name.IndexOf("luid_", StringComparison.Ordinal);
                var luid = at < 0 ? name : name.Substring(at, Math.Min(name.Length - at, 27));
                perGpu[luid] = perGpu.GetValueOrDefault(luid) + (entry.Value.CStatus == 0 ? entry.Value.DoubleValue : 0);
            }
            return perGpu.Count == 0 ? 0 : Math.Min(100, perGpu.Values.Max());
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public void Dispose()
    {
        if (query != IntPtr.Zero) PdhCloseQuery(query);
    }

    private const uint PdhFmtDouble = 0x00000200, PdhFmtNoCap100 = 0x00008000;
    private const int PdhMoreData = unchecked((int)0x800007D2);

    [StructLayout(LayoutKind.Sequential)] private struct PdhFmtCounterValue { public uint CStatus; public double DoubleValue; }
    [StructLayout(LayoutKind.Sequential)] private struct PdhFmtCounterValueItem { public IntPtr Name; public PdhFmtCounterValue Value; }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhOpenQuery(string? source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")] private static extern int PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")] private static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, IntPtr type, out PdhFmtCounterValue value);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern int PdhGetFormattedCounterArray(IntPtr counter, uint format, ref int bufferSize, out int itemCount, IntPtr items);
    [DllImport("pdh.dll")] private static extern int PdhCloseQuery(IntPtr query);
}
