using System.Diagnostics;
using System.Globalization;
using MngLogs.Agent;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Metrics;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Linux.Metrics;

/// <summary>Linux host metrics via /proc + DriveInfo + Process API.</summary>
public sealed class LinuxHostMetricsCollector : IHostMetricsCollector
{
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "idle",
        "kthreadd",
        "ksoftirqd",
        "migration",
        "rcu_sched",
        "rcu_bh",
        "watchdog"
    };

    private readonly IAgentConfigStore _config;
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> _prevCpu = new();
    private readonly object _topLock = new();
    private (ulong Idle, ulong Total)? _prevCpuStat;

    public LinuxHostMetricsCollector(IAgentConfigStore config)
    {
        _config = config;
    }

    public IReadOnlyList<IngestEventItem> Collect(bool includeHostResources)
    {
        var now = DateTime.UtcNow;
        var items = new List<IngestEventItem>
        {
            Metric(now, "up", 1, "host.up", BuildHostUpFields(ResolveLocalUi()))
        };

        if (!includeHostResources)
            return items;

        AddCpu(items, now);
        AddMemory(items, now);
        AddDisk(items, now);
        return items;
    }

    public HostInventorySnapshot CaptureInventory()
    {
        var localUi = ResolveLocalUi();
        var ips = HostNetworkAddresses.Collect();
        var primaryIp = HostNetworkAddresses.PickPrimary(ips);
        var (bootUtc, uptimeSec) = HostUptimeInfo.Capture();

        return new HostInventorySnapshot
        {
            CollectedAtUtc = DateTime.UtcNow,
            IpAddresses = ips,
            PrimaryIp = primaryIp,
            LoggedOnUsers = [],
            ConsoleUser = null,
            AgentVersion = AgentVersion.Current,
            BootTimeUtc = bootUtc,
            UptimeSeconds = uptimeSec,
            LocalUiPort = localUi.Port,
            LocalUiHost = localUi.Host,
            Sessions = []
        };
    }

    public TopProcessSnapshot CollectTopProcesses(int take)
    {
        take = Math.Clamp(take, 1, 15);
        var now = DateTime.UtcNow;
        var samples = new List<TopProcessItem>();
        var seen = new HashSet<int>();
        var hadPriorCpuSample = false;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string name;
                try { name = process.ProcessName; }
                catch { continue; }

                if (string.IsNullOrWhiteSpace(name) || ExcludedProcessNames.Contains(name))
                    continue;

                long workingSet;
                try { workingSet = process.WorkingSet64; }
                catch { continue; }

                double? cpuPercent = null;
                try
                {
                    var total = process.TotalProcessorTime;
                    lock (_topLock)
                    {
                        if (_prevCpu.TryGetValue(process.Id, out var prev))
                        {
                            hadPriorCpuSample = true;
                            var elapsed = (now - prev.At).TotalSeconds;
                            if (elapsed >= 0.5)
                            {
                                var cpuSec = (total - prev.Cpu).TotalSeconds;
                                var cores = Math.Max(1, Environment.ProcessorCount);
                                var pct = 100.0 * cpuSec / (elapsed * cores);
                                if (pct < 0) pct = 0;
                                if (pct > 100 * cores) pct = 100 * cores;
                                cpuPercent = Math.Round(pct, 1);
                            }
                        }

                        _prevCpu[process.Id] = (total, now);
                    }

                    seen.Add(process.Id);
                }
                catch
                {
                    // Access denied on some processes
                }

                samples.Add(new TopProcessItem
                {
                    Pid = process.Id,
                    Name = name,
                    CpuPercent = cpuPercent,
                    WorkingSetBytes = workingSet
                });
            }
            finally
            {
                process.Dispose();
            }
        }

        lock (_topLock)
        {
            foreach (var stale in _prevCpu.Keys.Where(id => !seen.Contains(id)).ToList())
                _prevCpu.Remove(stale);
        }

        var byMemory = samples.OrderByDescending(x => x.WorkingSetBytes).Take(take).ToArray();
        var withCpu = samples.Where(x => x.CpuPercent.HasValue).ToList();
        var cpuPending = !hadPriorCpuSample || withCpu.Count == 0;
        var byCpu = withCpu.OrderByDescending(x => x.CpuPercent!.Value).Take(take).ToArray();

        return new TopProcessSnapshot
        {
            AtUtc = now,
            ByCpu = byCpu,
            ByMemory = byMemory,
            CpuPending = cpuPending && byCpu.Length == 0
        };
    }

    public IReadOnlyList<IngestEventItem> ToTopProcessEvents(TopProcessSnapshot snapshot)
    {
        var items = new List<IngestEventItem>();

        if (snapshot.ByCpu.Count > 0)
        {
            var topCpu = snapshot.ByCpu[0].CpuPercent ?? 0;
            items.Add(Metric(
                snapshot.AtUtc,
                "process.top_cpu",
                topCpu,
                "process.top_cpu",
                new Dictionary<string, object?>
                {
                    ["event.action"] = "process.top_cpu",
                    ["count"] = snapshot.ByCpu.Count,
                    ["processes"] = snapshot.ByCpu
                        .Select(p => new Dictionary<string, object?>
                        {
                            ["name"] = p.Name,
                            ["pid"] = p.Pid,
                            ["cpuPercent"] = p.CpuPercent
                        })
                        .ToList()
                }));
        }

        if (snapshot.ByMemory.Count > 0)
        {
            var topWs = snapshot.ByMemory[0].WorkingSetBytes;
            items.Add(Metric(
                snapshot.AtUtc,
                "process.top_memory",
                topWs,
                "process.top_memory",
                new Dictionary<string, object?>
                {
                    ["event.action"] = "process.top_memory",
                    ["count"] = snapshot.ByMemory.Count,
                    ["processes"] = snapshot.ByMemory
                        .Select(p => new Dictionary<string, object?>
                        {
                            ["name"] = p.Name,
                            ["pid"] = p.Pid,
                            ["workingSetBytes"] = p.WorkingSetBytes
                        })
                        .ToList()
                }));
        }

        return items;
    }

    private LocalUiBindInfo ResolveLocalUi()
    {
        var s = _config.Current.System;
        var port = s.LocalUiPort <= 0 ? 5092 : s.LocalUiPort;
        var host = string.IsNullOrWhiteSpace(s.LocalUiHost) ? "127.0.0.1" : s.LocalUiHost.Trim();
        return new LocalUiBindInfo(port, host);
    }

    private Dictionary<string, object?> BuildHostUpFields(LocalUiBindInfo bind)
    {
        var ips = HostNetworkAddresses.Collect();
        var primaryIp = HostNetworkAddresses.PickPrimary(ips);
        var (bootUtc, uptimeSec) = HostUptimeInfo.Capture();

        return new Dictionary<string, object?>
        {
            ["os"] = Environment.OSVersion.ToString(),
            ["machine"] = Environment.MachineName,
            ["agentVersion"] = AgentVersion.Current,
            ["platform"] = "linux",
            ["ipAddresses"] = ips.ToList(),
            ["primaryIp"] = primaryIp,
            ["bootTimeUtc"] = bootUtc.ToString("o"),
            ["uptimeSeconds"] = uptimeSec,
            ["loggedOnUsers"] = new List<string>(),
            ["consoleUser"] = null,
            ["loggedOnSessions"] = new List<object>(),
            ["localUiPort"] = bind.Port,
            ["localUiHost"] = bind.Host,
            ["localUiRemoteAccess"] = IsRemoteAccessibleBind(bind.Host)
        };
    }

    private void AddCpu(List<IngestEventItem> items, DateTime now)
    {
        try
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault();
            if (line is null || !line.StartsWith("cpu ", StringComparison.Ordinal))
                return;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                return;

            ulong user = ParseULong(parts[1]);
            ulong nice = ParseULong(parts[2]);
            ulong system = ParseULong(parts[3]);
            ulong idle = ParseULong(parts[4]);
            ulong iowait = parts.Length > 5 ? ParseULong(parts[5]) : 0;
            ulong irq = parts.Length > 6 ? ParseULong(parts[6]) : 0;
            ulong softirq = parts.Length > 7 ? ParseULong(parts[7]) : 0;
            ulong steal = parts.Length > 8 ? ParseULong(parts[8]) : 0;

            var idleAll = idle + iowait;
            var total = user + nice + system + idleAll + irq + softirq + steal;

            if (_prevCpuStat is { } prev && total > prev.Total)
            {
                var idleDelta = idleAll - prev.Idle;
                var totalDelta = total - prev.Total;
                if (totalDelta > 0)
                {
                    var busy = 100.0 * (1.0 - (double)idleDelta / totalDelta);
                    if (busy < 0) busy = 0;
                    if (busy > 100) busy = 100;
                    items.Add(Metric(now, "cpu.percent", Math.Round(busy, 2), "host.cpu", null));
                }
            }

            _prevCpuStat = (idleAll, total);
        }
        catch
        {
            // /proc unavailable
        }
    }

    private static void AddMemory(List<IngestEventItem> items, DateTime now)
    {
        try
        {
            long memTotal = 0;
            long memAvailable = 0;
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    memTotal = ParseMemKb(line) * 1024;
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    memAvailable = ParseMemKb(line) * 1024;
            }

            if (memAvailable > 0)
            {
                items.Add(Metric(now, "memory.available_bytes", memAvailable, "host.memory",
                    new Dictionary<string, object?>
                    {
                        ["availableMb"] = Math.Round(memAvailable / (1024d * 1024d), 1),
                        ["totalBytes"] = memTotal > 0 ? memTotal : null
                    }));
            }

            items.Add(Metric(now, "memory.process_working_set_bytes",
                Process.GetCurrentProcess().WorkingSet64, "host.memory", null));
        }
        catch
        {
            items.Add(Metric(now, "memory.process_working_set_bytes",
                Process.GetCurrentProcess().WorkingSet64, "host.memory", null));
        }
    }

    private static void AddDisk(List<IngestEventItem> items, DateTime now)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Network or DriveType.Removable))
                    continue;

                // Skip pseudo / container noise mounts.
                var name = drive.Name.TrimEnd('/');
                if (name is "/proc" or "/sys" or "/dev" or "/run" or "/boot" or "/boot/efi")
                    continue;
                if (name.StartsWith("/snap", StringComparison.Ordinal) ||
                    name.StartsWith("/sys", StringComparison.Ordinal) ||
                    name.StartsWith("/proc", StringComparison.Ordinal) ||
                    name.StartsWith("/run", StringComparison.Ordinal) ||
                    name.StartsWith("/var/lib/docker", StringComparison.Ordinal) ||
                    name.Contains("/overlay", StringComparison.Ordinal) ||
                    name.Contains("/docker/", StringComparison.Ordinal))
                    continue;

                items.Add(Metric(now, "disk.free_bytes", drive.AvailableFreeSpace, "host.disk",
                    new Dictionary<string, object?>
                    {
                        ["volume"] = string.IsNullOrEmpty(name) ? "/" : name,
                        ["totalBytes"] = drive.TotalSize
                    }));
                items.Add(Metric(now, "disk.total_bytes", drive.TotalSize, "host.disk",
                    new Dictionary<string, object?>
                    {
                        ["volume"] = string.IsNullOrEmpty(name) ? "/" : name
                    }));
            }
            catch
            {
                // skip drive
            }
        }
    }

    private static bool IsRemoteAccessibleBind(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        var h = host.Trim();
        if (h is "0.0.0.0" or "*" or "::" or "[::]") return true;
        if (h is "127.0.0.1" or "localhost" or "::1") return false;
        return true;
    }

    private static ulong ParseULong(string s) =>
        ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static long ParseMemKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 &&
               long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kb)
            ? kb
            : 0;
    }

    private static IngestEventItem Metric(
        DateTime now,
        string metric,
        double value,
        string message,
        Dictionary<string, object?>? extra)
    {
        var fields = new Dictionary<string, object?>
        {
            ["metric"] = metric,
            ["value"] = value
        };
        if (extra != null)
        {
            foreach (var kv in extra)
                fields[kv.Key] = kv.Value;
        }

        return new IngestEventItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = now,
            Source = "metric",
            SourceProduct = "mnglogs-agent",
            Severity = "info",
            Message = message,
            Fields = fields
        };
    }
}
