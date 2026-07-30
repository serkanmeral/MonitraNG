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

/// <summary>Local / MSI-configured values (CLI recovery).</summary>
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

    /// <summary>Root for queue + persisted config overrides. Empty = %ProgramData%\MngLogs\Agent</summary>
    public string DataDirectory { get; set; } = string.Empty;
}

/// <summary>Policy (intervals, domain, packages). File-backed now; server pull later.</summary>
public sealed class PolicyConfig
{
    public string Domain { get; set; } = "default";

    /// <summary>Host metrics interval (includes up=1).</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 60;

    public int ShipIntervalSeconds { get; set; } = 5;

    public int MaxEventsPerBatch { get; set; } = 100;

    public MetricsPolicy Metrics { get; set; } = new();

    public EventLogPolicy EventLog { get; set; } = new();

    public ServiceWatchPolicy ServiceWatch { get; set; } = new();
}

public sealed class MetricsPolicy
{
    public bool Enabled { get; set; } = true;

    /// <summary>Collect CPU / memory / disk besides host.up.</summary>
    public bool IncludeHostResources { get; set; } = true;

    /// <summary>Collect top CPU/RAM processes for local UI and Phase-1 metric ship.</summary>
    public bool IncludeTopProcesses { get; set; } = true;

    /// <summary>How many processes to keep per ranking (CPU and memory).</summary>
    public int TopProcessCount { get; set; } = 5;
}

public sealed class EventLogPolicy
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxEventsPerPoll { get; set; } = 50;

    /// <summary>
    /// Legacy full package list. When non-empty and AgentOverrides/DisabledServerPackages are unused,
    /// Resolve treats this as the sole effective set (pre-merge behavior). Prefer AgentOverrides.
    /// </summary>
    public List<EventLogPackage> Packages { get; set; } = [];

    /// <summary>
    /// Agent-specific packages: same name replaces a server package; new names are added.
    /// </summary>
    public List<EventLogPackage> AgentOverrides { get; set; } = [];

    /// <summary>Server catalog package names to exclude from the effective set.</summary>
    public List<string> DisabledServerPackages { get; set; } = [];

    /// <summary>How often to refresh the cached server package catalog (seconds).</summary>
    public int PackageCatalogSyncIntervalSeconds { get; set; } = 3600;
}

public sealed class EventLogPackage
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Windows channel, e.g. Security, System, Application.</summary>
    public string Channel { get; set; } = string.Empty;

    public List<int> EventIds { get; set; } = [];
}

public sealed class ServiceWatchPolicy
{
    public bool Enabled { get; set; } = true;

    public int PollIntervalSeconds { get; set; } = 15;

    /// <summary>Minimum seconds between restart attempts for the same service.</summary>
    public int RestartCooldownSeconds { get; set; } = 300;

    /// <summary>Max restart attempts per unhealthy incident (resets on recovered).</summary>
    public int RestartMaxAttempts { get; set; } = 3;

    /// <summary>Ship periodic watch.inventory summaries (metric-style) for central dashboards.</summary>
    public bool IncludeInventory { get; set; } = true;

    /// <summary>How often to ship watch.inventory (throttled; independent of transition events).</summary>
    public int InventoryIntervalSeconds { get; set; } = 60;

    /// <summary>Windows service names to monitor (ServiceName, not display name).</summary>
    public List<WatchedService> Services { get; set; } = [];

    /// <summary>Process/application names to monitor (ProcessName, with or without .exe).</summary>
    public List<WatchedApplication> Applications { get; set; } = [];
}

public sealed class WatchedService
{
    /// <summary>Service name as registered with SCM (e.g. Spooler, wuauserv).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When true, agent attempts ServiceController.Start on failure (may need elevation).</summary>
    public bool RestartAllowed { get; set; }
}

public sealed class WatchedApplication
{
    /// <summary>Process name to match (e.g. notepad or notepad.exe).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Minimum running instance count to consider healthy.</summary>
    public int MinCount { get; set; } = 1;

    /// <summary>When true, agent starts ExecutablePath when instance count is below MinCount.</summary>
    public bool RestartAllowed { get; set; }

    /// <summary>Full path to executable used for restart (required when RestartAllowed).</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Optional arguments passed to ExecutablePath.</summary>
    public string? Arguments { get; set; }

    /// <summary>Optional working directory for Process.Start.</summary>
    public string? WorkingDirectory { get; set; }
}
