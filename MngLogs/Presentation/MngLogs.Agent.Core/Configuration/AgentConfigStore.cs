using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MngLogs.Agent.Configuration;

public interface IAgentConfigStore
{
    MngLogsAgentSettings Current { get; }
    string ResolveDataDirectory();
    string ResolveConfigDirectory();
    string ResolveHostId();
    Task SaveSystemAsync(SystemConfig system, CancellationToken cancellationToken = default);
    Task SavePolicyAsync(PolicyConfig policy, CancellationToken cancellationToken = default);
}

/// <summary>Merges appsettings with optional JSON files under ConfigDirectory (system.json / policy.json).</summary>
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
            return configured.Trim();

        return PlatformPaths.DefaultDataDirectory();
    }

    public string ResolveConfigDirectory()
    {
        var configured = Current.System.ConfigDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        return PlatformPaths.DefaultConfigDirectory(ResolveDataDirectory());
    }

    public string ResolveHostId()
    {
        var id = Current.System.HostId;
        if (!string.IsNullOrWhiteSpace(id))
            return id.Trim();

        return Environment.MachineName;
    }

    private void EnsureDefaultHostId()
    {
        if (!string.IsNullOrWhiteSpace(_current.System.HostId))
            return;

        _current.System.HostId = Environment.MachineName;
        try
        {
            var dir = ResolveConfigDirectoryUnlocked();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "system.json");
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
                toSave.DataDirectory = ResolveDataDirectoryUnlocked();
            if (string.IsNullOrWhiteSpace(toSave.ConfigDirectory) && OperatingSystem.IsLinux())
                toSave.ConfigDirectory = PlatformPaths.LinuxConfigDirectory;

            File.WriteAllText(path, JsonSerializer.Serialize(toSave, JsonOptions));
            _current.System = CloneSystem(toSave);
        }
        catch
        {
            // In-memory MachineName still used
        }
    }

    public async Task SaveSystemAsync(SystemConfig system, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (string.IsNullOrWhiteSpace(system.HostId))
            system.HostId = Environment.MachineName;

        var dir = ResolveConfigDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "system.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(system, JsonOptions), cancellationToken);

        lock (_gate)
            _current.System = CloneSystem(system);
    }

    public async Task SavePolicyAsync(PolicyConfig policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var dir = ResolveConfigDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "policy.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(policy, JsonOptions), cancellationToken);

        lock (_gate)
            _current.Policy = ClonePolicy(policy);
    }

    private void TryLoadOverrides()
    {
        var configDir = ResolveConfigDirectoryUnlocked();
        var dataDir = ResolveDataDirectoryUnlocked();

        // Prefer ConfigDirectory; fall back to DataDirectory (Windows-style single root).
        TryMerge(Path.Combine(configDir, "system.json"), json =>
        {
            var system = JsonSerializer.Deserialize<SystemConfig>(json, JsonOptions);
            if (system != null)
                _current.System = system;
        });
        if (!File.Exists(Path.Combine(configDir, "system.json")) &&
            !string.Equals(configDir, dataDir, StringComparison.OrdinalIgnoreCase))
        {
            TryMerge(Path.Combine(dataDir, "system.json"), json =>
            {
                var system = JsonSerializer.Deserialize<SystemConfig>(json, JsonOptions);
                if (system != null)
                    _current.System = system;
            });
        }

        TryMerge(Path.Combine(configDir, "policy.json"), json => ApplyPolicyJson(json));
        if (!File.Exists(Path.Combine(configDir, "policy.json")) &&
            !string.Equals(configDir, dataDir, StringComparison.OrdinalIgnoreCase))
        {
            TryMerge(Path.Combine(dataDir, "policy.json"), json => ApplyPolicyJson(json));
        }
    }

    private void ApplyPolicyJson(string json)
    {
        var policy = JsonSerializer.Deserialize<PolicyConfig>(json, JsonOptions);
        if (policy == null)
            return;

        policy.Metrics ??= new MetricsPolicy();
        policy.EventLog ??= new EventLogPolicy();
        policy.Journal ??= new JournalPolicy();
        policy.ServiceWatch ??= new ServiceWatchPolicy();
        policy.EventLog.Packages ??= [];
        policy.EventLog.AgentOverrides ??= [];
        policy.EventLog.DisabledServerPackages ??= [];
        if (policy.EventLog.PackageCatalogSyncIntervalSeconds <= 0)
            policy.EventLog.PackageCatalogSyncIntervalSeconds = 3600;
        policy.Journal.DisabledPackages ??= [];
        policy.Journal.Packages ??= [];
        if (policy.Journal.PollIntervalSeconds <= 0)
            policy.Journal.PollIntervalSeconds = 10;
        if (policy.Journal.MaxEventsPerPoll <= 0)
            policy.Journal.MaxEventsPerPoll = 50;
        policy.ServiceWatch.Services ??= [];
        policy.ServiceWatch.Applications ??= [];
        _current.Policy = policy;
    }

    private string ResolveDataDirectoryUnlocked()
    {
        var configured = _current.System.DataDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();
        return PlatformPaths.DefaultDataDirectory();
    }

    private string ResolveConfigDirectoryUnlocked()
    {
        var configured = _current.System.ConfigDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();
        return PlatformPaths.DefaultConfigDirectory(ResolveDataDirectoryUnlocked());
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
        DataDirectory = s.DataDirectory,
        ConfigDirectory = s.ConfigDirectory
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
                    EventIds = [.. x.EventIds],
                    IsDefault = x.IsDefault
                })
                .ToList(),
            AgentOverrides = (p.EventLog.AgentOverrides ?? [])
                .Select(x => new EventLogPackage
                {
                    Name = x.Name,
                    Channel = x.Channel,
                    EventIds = [.. x.EventIds],
                    IsDefault = x.IsDefault
                })
                .ToList(),
            DisabledServerPackages = (p.EventLog.DisabledServerPackages ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        },
        Journal = new JournalPolicy
        {
            Enabled = (p.Journal ?? new JournalPolicy()).Enabled,
            PollIntervalSeconds = (p.Journal?.PollIntervalSeconds ?? 10) <= 0 ? 10 : p.Journal!.PollIntervalSeconds,
            MaxEventsPerPoll = (p.Journal?.MaxEventsPerPoll ?? 50) <= 0 ? 50 : p.Journal!.MaxEventsPerPoll,
            DisabledPackages = (p.Journal?.DisabledPackages ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Packages = (p.Journal?.Packages ?? [])
                .Select(x => new JournalPackage
                {
                    Name = x.Name,
                    Unit = x.Unit,
                    Identifier = x.Identifier,
                    Grep = x.Grep,
                    Priority = x.Priority,
                    IsDefault = x.IsDefault
                })
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
                    Name = ProcessNameNormalizer.Normalize(x.Name),
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
        }
    };
}
