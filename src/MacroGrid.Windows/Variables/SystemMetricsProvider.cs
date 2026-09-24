using System.Runtime.InteropServices;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Windows.Variables;

/// <summary>
/// Built-in "system.*" variables: time, uptime, CPU and RAM usage. Sampled once a second, aligned to the
/// wall-clock second so <c>system.time</c> never looks stale between ticks. No PerformanceCounter dependency:
/// CPU comes from GetSystemTimes deltas, RAM from GlobalMemoryStatusEx.
/// </summary>
public sealed class SystemMetricsProvider : IVariableProvider, IVariableCatalogSource
{
    private const string Category = "System";

    public IEnumerable<VariableInfo> Describe() =>
    [
        new("system.time", "Current time and date", "{system.time|HH:mm:ss}", Category),
        new("system.uptime", "How long the computer has been running", "{system.uptime}", Category),
        new("system.cpu", "CPU usage (%)", "{system.cpu|0}%", Category),
        new("system.ram", "RAM usage (%)", "{system.ram|0}%", Category),
        new("system.ram.used", "Used RAM (GB)", "{system.ram.used|0.#} GB", Category),
        new("system.ram.total", "Total RAM (GB)", "{system.ram.total|0.#} GB", Category),
    ];

    public async Task RunAsync(IVariableStore store, CancellationToken cancellationToken)
    {
        GetTimes(out var lastIdle, out var lastKernel, out var lastUser);

        await Task.Delay(1000 - DateTime.Now.Millisecond, cancellationToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            store.Set("system.time", DateTime.Now);
            store.Set("system.uptime", TimeSpan.FromMilliseconds(Environment.TickCount64));

            if (GetTimes(out var idle, out var kernel, out var user))
            {
                var idleDelta = idle - lastIdle;
                var totalDelta = (kernel - lastKernel) + (user - lastUser);
                if (totalDelta > 0)
                    store.Set("system.cpu", Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100));
                (lastIdle, lastKernel, lastUser) = (idle, kernel, user);
            }

            if (GetMemory(out var totalMb, out var availMb) && totalMb > 0)
            {
                store.Set("system.ram", Math.Clamp((totalMb - availMb) * 100.0 / totalMb, 0, 100));
                store.Set("system.ram.used", (totalMb - availMb) / 1024.0);
                store.Set("system.ram.total", totalMb / 1024.0);
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private static bool GetTimes(out long idle, out long kernel, out long user)
    {
        idle = kernel = user = 0;
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return false;
        idle = ToLong(idleFt);
        kernel = ToLong(kernelFt);
        user = ToLong(userFt);
        return true;
    }

    private static long ToLong(FILETIME ft) => ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    private static bool GetMemory(out double totalMb, out double availMb)
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            totalMb = availMb = 0;
            return false;
        }
        totalMb = status.ullTotalPhys / 1024.0 / 1024.0;
        availMb = status.ullAvailPhys / 1024.0 / 1024.0;
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
