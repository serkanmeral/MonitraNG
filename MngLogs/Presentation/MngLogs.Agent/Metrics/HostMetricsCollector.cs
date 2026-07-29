using System.Diagnostics;
using System.Runtime.Versioning;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Metrics;

public interface IHostMetricsCollector
{
    IReadOnlyList<IngestEventItem> Collect(bool includeHostResources);

    /// <summary>Top CPU/RAM snapshot for local status and collector summaries.</summary>
    TopProcessSnapshot CollectTopProcesses(int take);

    /// <summary>Summary ingest events: process.top_cpu / process.top_memory (no per-process flood).</summary>
    IReadOnlyList<IngestEventItem> ToTopProcessEvents(TopProcessSnapshot snapshot);
}

public sealed class HostMetricsCollector : IHostMetricsCollector
{
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Idle",
        "System",
        "Registry",
        "Memory Compression",
        "Secure System"
    };

    private PerformanceCounter? _cpuCounter;
    private bool _cpuWarmed;
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> _prevCpu = new();
    private readonly object _topLock = new();

    public IReadOnlyList<IngestEventItem> Collect(bool includeHostResources)
    {
        var now = DateTime.UtcNow;
        var items = new List<IngestEventItem>
        {
            Metric(now, "up", 1, "host.up", new Dictionary<string, object?>
            {
                ["os"] = Environment.OSVersion.ToString(),
                ["machine"] = Environment.MachineName
            })
        };

        if (!includeHostResources)
            return items;

        if (OperatingSystem.IsWindows())
            AddWindowsResources(items, now);
        else
            AddPortableDisk(items, now);

        return items;
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
                try
                {
                    name = process.ProcessName;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name) || ExcludedProcessNames.Contains(name))
                    continue;

                long workingSet;
                try
                {
                    workingSet = process.WorkingSet64;
                }
                catch
                {
                    continue;
                }

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
                    // Access denied for TotalProcessorTime on some protected processes.
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

        var byMemory = samples
            .OrderByDescending(x => x.WorkingSetBytes)
            .Take(take)
            .ToArray();

        var withCpu = samples.Where(x => x.CpuPercent.HasValue).ToList();
        var cpuPending = !hadPriorCpuSample || withCpu.Count == 0;
        var byCpu = withCpu
            .OrderByDescending(x => x.CpuPercent!.Value)
            .Take(take)
            .ToArray();

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

    [SupportedOSPlatform("windows")]
    private void AddWindowsResources(List<IngestEventItem> items, DateTime now)
    {
        try
        {
            _cpuCounter ??= new PerformanceCounter("Processor", "% Processor Time", "_Total");
            var cpu = _cpuCounter.NextValue();
            if (!_cpuWarmed)
            {
                _cpuWarmed = true;
                // First sample is typically 0; second call after short delay is more accurate — caller polls periodically.
            }
            else
            {
                items.Add(Metric(now, "cpu.percent", Math.Round(cpu, 2), "host.cpu", null));
            }
        }
        catch
        {
            // Performance counters may be unavailable in some sandboxes.
        }

        try
        {
            using var avail = new PerformanceCounter("Memory", "Available MBytes");
            var availableMb = avail.NextValue();
            var availableBytes = (long)(availableMb * 1024d * 1024d);

            items.Add(Metric(now, "memory.available_bytes", availableBytes, "host.memory", new Dictionary<string, object?>
            {
                ["availableMb"] = Math.Round(availableMb, 1)
            }));
            items.Add(Metric(now, "memory.process_working_set_bytes",
                Process.GetCurrentProcess().WorkingSet64, "host.memory", null));
        }
        catch
        {
            items.Add(Metric(now, "memory.process_working_set_bytes",
                Process.GetCurrentProcess().WorkingSet64, "host.memory", null));
        }

        AddPortableDisk(items, now);
    }

    private static void AddPortableDisk(List<IngestEventItem> items, DateTime now)
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                    continue;

                var name = drive.Name.TrimEnd('\\', '/');
                items.Add(Metric(now, "disk.free_bytes", drive.AvailableFreeSpace, "host.disk",
                    new Dictionary<string, object?>
                    {
                        ["volume"] = name,
                        ["totalBytes"] = drive.TotalSize
                    }));
                items.Add(Metric(now, "disk.total_bytes", drive.TotalSize, "host.disk",
                    new Dictionary<string, object?> { ["volume"] = name }));
            }
            catch
            {
                // skip drive
            }
        }
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
