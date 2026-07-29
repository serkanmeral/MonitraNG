using System.Diagnostics;
using System.Runtime.Versioning;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Metrics;

public interface IHostMetricsCollector
{
    IReadOnlyList<IngestEventItem> Collect(bool includeHostResources);
}

public sealed class HostMetricsCollector : IHostMetricsCollector
{
    private PerformanceCounter? _cpuCounter;
    private bool _cpuWarmed;

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
