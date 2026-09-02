using System.Text.Json;
using Microsoft.Extensions.Options;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Agent.Configuration;

public interface IAgentConfigStore
{
    MngLogsAgentSettings Current { get; }
    string ResolveDataDirectory();
    string ResolveHostId();
    Task SaveSystemAsync(SystemConfig system, CancellationToken cancellationToken = default);
    Task SavePolicyAsync(PolicyConfig policy, CancellationToken cancellationToken = default);
}

/// <summary>Merges appsettings with optional JSON files under DataDirectory (system.json / policy.json).</summary>
public sealed class AgentConfigStore : IAgentConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _gate = new();
    private MngLogsAgentSettings _current;

    public AgentConfigStore(IOptions<MngLogsAgentSettings> options)
    {
        _current = Clone(options.Value);
        TryLoadOverrides();
        EnsureDefaultHostId();
    }

    public MngLogsAgentSettings Current
    {
        get
        {
            lock (_gate)
                return Clone(_current);
        }
    }

    public string ResolveDataDirectory()
    {
        var configured = Current.System.DataDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
            programData = Path.Combine(Path.GetTempPath(), "MngLogs");

        return Path.Combine(programData, "MngLogs", "Agent");
    }

    public string ResolveHostId()
    {
        var id = Current.System.HostId;
        if (!string.IsNullOrWhiteSpace(id))
            return id.Trim();

        return Environment.MachineName;
    }

    /// <summary>
    /// First install / empty HostId → persist machine name so Local UI and MSI leave a concrete value.
    /// </summary>
    private void EnsureDefaultHostId()
    {
        if (!string.IsNullOrWhiteSpace(_current.System.HostId))
            return;

        _current.System.HostId = Environment.MachineName;
        try
        {
            var dir = ResolveDataDirectoryUnlocked();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "system.json");
            // Merge into existing system.json when present; otherwise write HostId + current system snapshot.
            SystemConfig toSave = CloneSystem(_current.System);
            if (File.Exists(path))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<SystemConfig>(File.ReadAllText(path), JsonOptions);
                    if (existing is not null)
                    {
                        toSave = existing;
                        if (string.IsNullOrWhiteSpace(toSave.HostId))
                            toSave.HostId = Environment.MachineName;
                    }
                }
                catch
                {
                    // keep toSave from memory
                }
            }

            if (string.IsNullOrWhiteSpace(toSave.DataDirectory))
                toSave.DataDirectory = dir;

            File.WriteAllText(path, JsonSerializer.Serialize(toSave, JsonOptions));
            _current.System = CloneSystem(toSave);
        }
        catch
        {
            // In-memory MachineName still used via HostId assignment above
        }
    }

    public async Task SaveSystemAsync(SystemConfig system, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (string.IsNullOrWhiteSpace(system.HostId))
            system.HostId = Environment.MachineName;

        var dir = ResolveDataDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "system.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(system, JsonOptions), cancellationToken);

        lock (_gate)
            _current.System = CloneSystem(system);
    }

    public async Task SavePolicyAsync(PolicyConfig policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var dir = ResolveDataDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "policy.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(policy, JsonOptions), cancellationToken);

        lock (_gate)
            _current.Policy = ClonePolicy(policy);
    }

    private void TryLoadOverrides()
    {
        var dir = ResolveDataDirectoryUnlocked();
        TryMerge(Path.Combine(dir, "system.json"), json =>
        {
            var system = JsonSerializer.Deserialize<SystemConfig>(json, JsonOptions);
            if (system != null)
                _current.System = system;
        });
        TryMerge(Path.Combine(dir, "policy.json"), json =>
        {
            var policy = JsonSerializer.Deserialize<PolicyConfig>(json, JsonOptions);
            if (policy != null)
            {
                policy.Metrics ??= new MetricsPolicy();
                policy.EventLog ??= new EventLogPolicy();
                policy.ServiceWatch ??= new ServiceWatchPolicy();
                policy.EventLog.Packages ??= [];
                policy.EventLog.AgentOverrides ??= [];
                policy.EventLog.DisabledServerPackages ??= [];
                if (policy.EventLog.PackageCatalogSyncIntervalSeconds <= 0)
                    policy.EventLog.PackageCatalogSyncIntervalSeconds = 3600;
                policy.ServiceWatch.Services ??= [];
                policy.ServiceWatch.Applications ??= [];
                _current.Policy = policy;
            }
        });
    }

    private string ResolveDataDirectoryUnlocked()
    {
        var configured = _current.System.DataDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(programData))
            programData = Path.Combine(Path.GetTempPath(), "MngLogs");

        return Path.Combine(programData, "MngLogs", "Agent");
    }

    private static void TryMerge(string path, Action<string> apply)
    {
        if (!File.Exists(path))
            return;
        try
        {
            apply(File.ReadAllText(path));
        }
        catch
        {
            // Corrupt override: keep defaults
        }
    }

    private static MngLogsAgentSettings Clone(MngLogsAgentSettings s) => new()
    {
        System = CloneSystem(s.System),
        Policy = ClonePolicy(s.Policy)
    };

    private static SystemConfig CloneSystem(SystemConfig s) => new()
    {
        CollectorBaseUrl = s.CollectorBaseUrl,
        ApiKey = s.ApiKey,
        HostId = s.HostId,
        LocalUiHost = s.LocalUiHost,
        LocalUiPort = s.LocalUiPort,
        DataDirectory = s.DataDirectory
    };

    private static PolicyConfig ClonePolicy(PolicyConfig p) => new()
    {
        Domain = p.Domain,
        HeartbeatIntervalSeconds = p.HeartbeatIntervalSeconds,
        ShipIntervalSeconds = p.ShipIntervalSeconds,
        MaxEventsPerBatch = p.MaxEventsPerBatch,
        Metrics = new MetricsPolicy
        {
            Enabled = p.Metrics.Enabled,
            IncludeHostResources = p.Metrics.IncludeHostResources,
            IncludeTopProcesses = p.Metrics.IncludeTopProcesses,
            TopProcessCount = p.Metrics.TopProcessCount
        },
        EventLog = new EventLogPolicy
        {
            Enabled = p.EventLog.Enabled,
            PollIntervalSeconds = p.EventLog.PollIntervalSeconds,
            MaxEventsPerPoll = p.EventLog.MaxEventsPerPoll,
            PackageCatalogSyncIntervalSeconds = p.EventLog.PackageCatalogSyncIntervalSeconds <= 0
                ? 3600
                : p.EventLog.PackageCatalogSyncIntervalSeconds,
            Packages = (p.EventLog.Packages ?? [])
                .Select(x => new EventLogPackage
                {
                    Name = x.Name,
                    Channel = x.Channel,
                    EventIds = [.. x.EventIds]
                })
                .ToList(),
            AgentOverrides = (p.EventLog.AgentOverrides ?? [])
                .Select(x => new EventLogPackage
                {
                    Name = x.Name,
                    Channel = x.Channel,
                    EventIds = [.. x.EventIds]
                })
                .ToList(),
            DisabledServerPackages = (p.EventLog.DisabledServerPackages ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        },
        ServiceWatch = new ServiceWatchPolicy
        {
            Enabled = p.ServiceWatch.Enabled,
            PollIntervalSeconds = p.ServiceWatch.PollIntervalSeconds,
            RestartCooldownSeconds = p.ServiceWatch.RestartCooldownSeconds <= 0
                ? 300
                : p.ServiceWatch.RestartCooldownSeconds,
            RestartMaxAttempts = p.ServiceWatch.RestartMaxAttempts <= 0
                ? 3
                : p.ServiceWatch.RestartMaxAttempts,
            IncludeInventory = p.ServiceWatch.IncludeInventory,
            InventoryIntervalSeconds = p.ServiceWatch.InventoryIntervalSeconds <= 0
                ? 60
                : p.ServiceWatch.InventoryIntervalSeconds,
            Services = p.ServiceWatch.Services
                .Select(x => new WatchedService
                {
                    Name = x.Name,
                    RestartAllowed = x.RestartAllowed
                })
                .ToList(),
            Applications = p.ServiceWatch.Applications
                .Select(x => new WatchedApplication
                {
                    Name = NormalizeWatchedProcessName(x.Name),
                    MinCount = x.MinCount <= 0 ? 1 : x.MinCount,
                    RestartAllowed = x.RestartAllowed,
                    ExecutablePath = string.IsNullOrWhiteSpace(x.ExecutablePath) ? null : x.ExecutablePath.Trim(),
                    Arguments = string.IsNullOrWhiteSpace(x.Arguments) ? null : x.Arguments.Trim(),
                    WorkingDirectory = string.IsNullOrWhiteSpace(x.WorkingDirectory) ? null : x.WorkingDirectory.Trim()
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .ToList()
        },
        Dlp = new DlpAgentPolicy
        {
            EnforcementMode = string.IsNullOrWhiteSpace(p.Dlp?.EnforcementMode)
                ? "auditOnly"
                : p.Dlp.EnforcementMode.Trim(),
            PolicySyncIntervalSeconds = p.Dlp?.PolicySyncIntervalSeconds is > 0 and var sec
                ? sec
                : 3600
        }
    };

    private static string NormalizeWatchedProcessName(string? name) =>
        ApplicationWatchProbe.NormalizeProcessName(name ?? string.Empty);
}
