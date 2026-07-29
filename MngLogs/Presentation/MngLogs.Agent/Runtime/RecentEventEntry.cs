namespace MngLogs.Agent.Runtime;

public sealed class RecentEventEntry
{
    public DateTime AtUtc { get; init; }
    /// <summary>produced | shipped</summary>
    public string Direction { get; init; } = "produced";
    public string Source { get; init; } = "unknown";
    public string? Severity { get; init; }
    public string? Message { get; init; }
    public string? Action { get; init; }
    public string? MetricName { get; init; }
    public double? MetricValue { get; init; }
    public string? Detail { get; init; }
}

public sealed class LatestMetricItem
{
    public string Name { get; init; } = string.Empty;
    public double Value { get; init; }
    public string? Message { get; init; }
    public string? Detail { get; init; }
    public DateTime AtUtc { get; init; }
}

public sealed class ServiceWatchSnapshotItem
{
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Health { get; init; } = "Unknown";
    public string? StatusText { get; init; }
    public bool RestartAllowed { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class TopProcessItem
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public double? CpuPercent { get; init; }
    public long WorkingSetBytes { get; init; }
}

public sealed class TopProcessSnapshot
{
    public DateTime AtUtc { get; init; }
    public IReadOnlyList<TopProcessItem> ByCpu { get; init; } = [];
    public IReadOnlyList<TopProcessItem> ByMemory { get; init; } = [];
    /// <summary>True when CPU deltas need a prior sample (first cycle).</summary>
    public bool CpuPending { get; init; }
}
