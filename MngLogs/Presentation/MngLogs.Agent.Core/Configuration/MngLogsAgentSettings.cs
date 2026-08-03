namespace MngLogs.Agent.Configuration;

/// <summary>
/// Agent settings. System = machine-local (collector URL, ports). Policy = pullable later from server.
/// </summary>
public sealed class MngLogsAgentSettings
{
    public const string SectionName = "MngLogsAgentSettings";

    public SystemConfig System { get; set; } = new();
    public PolicyConfig Policy { get; set; } = new();
}

/// <summary>Local / package-configured values (CLI recovery).</summary>
public sealed class SystemConfig
{
    /// <summary>Collector base URL, e.g. https://siem.customer.local:5091</summary>
    public string CollectorBaseUrl { get; set; } = "http://127.0.0.1:5091";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Stable host id; empty = derive from machine name.</summary>
    public string HostId { get; set; } = string.Empty;

    /// <summary>Local status UI bind (loopback recommended).</summary>
    public string LocalUiHost { get; set; } = "127.0.0.1";

    public int LocalUiPort { get; set; } = 5092;

    /// <summary>
    /// Root for queue + logs + bookmarks.
    /// Empty = Windows ProgramData\MngLogs\Agent or Linux /var/lib/mnglogs/agent.
    /// </summary>
    public string DataDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Optional config root (system.json / policy.json).
    /// Empty = DataDirectory. Linux packages may use /etc/mnglogs/agent.
    /// </summary>
    public string ConfigDirectory { get; set; } = string.Empty;
}

/// <summary>Policy (intervals, domain, packages). File-backed now; server pull later.</summary>
public sealed class PolicyConfig
{
    public string Domain { get; set; } = "default";

    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public int ShipIntervalSeconds { get; set; } = 5;

    public int MaxEventsPerBatch { get; set; } = 100;

    public MetricsPolicy Metrics { get; set; } = new();

    public EventLogPolicy EventLog { get; set; } = new();

    /// <summary>Linux journald packages (P3c). Ignored on Windows.</summary>
    public JournalPolicy Journal { get; set; } = new();

    public ServiceWatchPolicy ServiceWatch { get; set; } = new();
}

public sealed class JournalPolicy
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxEventsPerPoll { get; set; } = 50;

    /// <summary>Builtin package names to disable (sshd, sudo, unit-fail).</summary>
    public List<string> DisabledPackages { get; set; } = [];

    /// <summary>Optional agent overrides / extra packages.</summary>
    public List<JournalPackage> Packages { get; set; } = [];
}

public sealed class JournalPackage
{
    public string Name { get; set; } = string.Empty;

    /// <summary>systemctl unit filter (-u), e.g. ssh.service</summary>
    public string? Unit { get; set; }

    /// <summary>SYSLOG_IDENTIFIER= filter</summary>
    public string? Identifier { get; set; }

    /// <summary>journalctl --grep pattern</summary>
    public string? Grep { get; set; }

    /// <summary>Priority ceiling for journalctl -p (e.g. err).</summary>
    public string? Priority { get; set; }

    public bool IsDefault { get; set; } = true;
}

public sealed class MetricsPolicy
{
    public bool Enabled { get; set; } = true;

    public bool IncludeHostResources { get; set; } = true;

    public bool IncludeTopProcesses { get; set; } = true;

    public int TopProcessCount { get; set; } = 5;
}

public sealed class EventLogPolicy
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxEventsPerPoll { get; set; } = 50;

    public List<EventLogPackage> Packages { get; set; } = [];

    public List<EventLogPackage> AgentOverrides { get; set; } = [];

    public List<string> DisabledServerPackages { get; set; } = [];

    public int PackageCatalogSyncIntervalSeconds { get; set; } = 3600;
}

public sealed class EventLogPackage
{
    public string Name { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    /// <summary><c>selected</c> (include EventIds) or <c>all</c> (channel minus ExcludedEventIds).</summary>
    public string SelectionMode { get; set; } = "selected";

    public List<int> EventIds { get; set; } = [];

    public List<int> ExcludedEventIds { get; set; } = [];

    public bool IsDefault { get; set; } = true;

    public bool IsAllChannel =>
        string.Equals(SelectionMode?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
}

public sealed class ServiceWatchPolicy
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 15;

    public int RestartCooldownSeconds { get; set; } = 300;

    public int RestartMaxAttempts { get; set; } = 3;

    public bool IncludeInventory { get; set; } = true;

    public int InventoryIntervalSeconds { get; set; } = 60;

    public List<WatchedService> Services { get; set; } = [];

    public List<WatchedApplication> Applications { get; set; } = [];
}

public sealed class WatchedService
{
    /// <summary>Windows SCM name or Linux systemd unit (e.g. nginx.service).</summary>
    public string Name { get; set; } = string.Empty;

    public bool RestartAllowed { get; set; }
}

public sealed class WatchedApplication
{
    public string Name { get; set; } = string.Empty;

    public int MinCount { get; set; } = 1;

    public bool RestartAllowed { get; set; }

    public string? ExecutablePath { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }
}
