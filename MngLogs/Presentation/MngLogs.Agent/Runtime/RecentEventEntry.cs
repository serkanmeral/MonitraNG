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
