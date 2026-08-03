namespace MngLogs.Agent.Metrics;

public sealed class HostInventorySnapshot
{
    public DateTime CollectedAtUtc { get; init; }
    public IReadOnlyList<string> IpAddresses { get; init; } = [];
    public string? PrimaryIp { get; init; }
    public IReadOnlyList<string> LoggedOnUsers { get; init; } = [];
    public string? ConsoleUser { get; init; }
    public string AgentVersion { get; init; } = string.Empty;
    public DateTime? BootTimeUtc { get; init; }
    public long? UptimeSeconds { get; init; }
    public int? LocalUiPort { get; init; }
    public string? LocalUiHost { get; init; }
    public IReadOnlyList<HostSessionSnapshot> Sessions { get; init; } = [];
}

public sealed class HostSessionSnapshot
{
    public string User { get; init; } = string.Empty;
    public int SessionId { get; init; }
    public string State { get; init; } = string.Empty;
    public string? StationName { get; init; }
    public string? ClientProtocol { get; init; }
    public DateTime? LogonAtUtc { get; init; }
    public long? DurationSeconds { get; init; }
}
