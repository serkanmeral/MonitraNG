using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MngLogs.Agent.Metrics;

/// <summary>Host boot time / uptime (GetTickCount64).</summary>
public static class HostUptimeInfo
{
    public static (DateTime BootTimeUtc, long UptimeSeconds) Capture()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var ms = GetTickCount64();
                var uptime = TimeSpan.FromMilliseconds(ms);
                var boot = DateTime.UtcNow - uptime;
                return (boot, (long)uptime.TotalSeconds);
            }
        }
        catch
        {
            // fall through
        }

        // Portable fallback (less accurate under long uptime / sleep on some hosts)
        var portableMs = Environment.TickCount64;
        if (portableMs < 0)
            portableMs = (long)(uint)Environment.TickCount;
        var portable = TimeSpan.FromMilliseconds(portableMs);
        return (DateTime.UtcNow - portable, (long)portable.TotalSeconds);
    }

    [DllImport("kernel32.dll")]
    [SupportedOSPlatform("windows")]
    private static extern ulong GetTickCount64();
}
