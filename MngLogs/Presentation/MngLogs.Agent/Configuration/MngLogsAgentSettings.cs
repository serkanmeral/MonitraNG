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

    /// <summary>Empty = built-in default packages (security-auth, system-lifecycle).</summary>
    public List<EventLogPackage> Packages { get; set; } = [];
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

    /// <summary>Windows service names to monitor (ServiceName, not display name).</summary>
    public List<WatchedService> Services { get; set; } = [];
}

public sealed class WatchedService
{
    /// <summary>Service name as registered with SCM (e.g. Spooler, wuauserv).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When true, agent attempts ServiceController.Start on failure (may need elevation).</summary>
    public bool RestartAllowed { get; set; }
}
