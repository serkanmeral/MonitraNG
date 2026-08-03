namespace MngLogs.Agent.Metrics;

/// <summary>Host boot time / uptime (Linux: /proc/uptime; elsewhere: TickCount64).</summary>
public static class HostUptimeInfo
{
    public static (DateTime BootTimeUtc, long UptimeSeconds) Capture()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                var line = File.ReadAllText("/proc/uptime").Trim();
                var space = line.IndexOf(' ');
                var token = space > 0 ? line[..space] : line;
                if (double.TryParse(token, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var seconds) &&
                    seconds >= 0)
                {
                    var uptime = TimeSpan.FromSeconds(seconds);
                    return (DateTime.UtcNow - uptime, (long)seconds);
                }
            }
            catch
            {
                // fall through
            }
        }

        var portableMs = Environment.TickCount64;
        if (portableMs < 0)
            portableMs = (long)(uint)Environment.TickCount;
        var portable = TimeSpan.FromMilliseconds(portableMs);
        return (DateTime.UtcNow - portable, (long)portable.TotalSeconds);
    }
}
