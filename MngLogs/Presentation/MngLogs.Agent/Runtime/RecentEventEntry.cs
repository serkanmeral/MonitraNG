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

    public string? Id { get; init; }
    public string? Channel { get; init; }
    public string? Package { get; init; }
    public int? EventId { get; init; }
    public long? RecordId { get; init; }
    public string? Provider { get; init; }
    /// <summary>Serialized Raw / reconstructed payload for detail modal.</summary>
    public string? RawJson { get; init; }
    public Dictionary<string, object?>? Fields { get; init; }
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
    /// <summary>service | application</summary>
    public string Kind { get; init; } = "service";
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string Health { get; init; } = "Unknown";
    public string? StatusText { get; init; }
    public bool RestartAllowed { get; init; }
    public int? InstanceCount { get; init; }
    public int? MinCount { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    /// <summary>Last correlated System SCM Event Log id (7031/7034/7036/…).</summary>
    public int? LastOsEventId { get; init; }
    public DateTime? LastOsEventAtUtc { get; init; }
    public string? LastOsEventAction { get; init; }
    public string? LastOsEventMessage { get; init; }

    public DateTime? LastRestartAtUtc { get; init; }
    public bool? LastRestartOk { get; init; }
    public string? LastRestartError { get; init; }
    public int RestartAttemptCount { get; init; }
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
