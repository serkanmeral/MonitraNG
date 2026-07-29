using System.Text.Json;
using Microsoft.Extensions.Options;
using MngLogs.Agent.Configuration;

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

    public async Task SaveSystemAsync(SystemConfig system, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(system);
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
                policy.ServiceWatch.Services ??= [];
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
            Packages = p.EventLog.Packages
                .Select(x => new EventLogPackage
                {
                    Name = x.Name,
                    Channel = x.Channel,
                    EventIds = [.. x.EventIds]
                })
                .ToList()
        },
        ServiceWatch = new ServiceWatchPolicy
        {
            Enabled = p.ServiceWatch.Enabled,
            PollIntervalSeconds = p.ServiceWatch.PollIntervalSeconds,
            Services = p.ServiceWatch.Services
                .Select(x => new WatchedService
                {
                    Name = x.Name,
                    RestartAllowed = x.RestartAllowed
                })
                .ToList()
        }
    };
}
