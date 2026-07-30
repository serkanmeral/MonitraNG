using System.Text.Json;
using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.EventLog;

public interface IEventLogPackageCatalogStore
{
    /// <summary>builtin | cache (future: collector).</summary>
    string Source { get; }
    DateTime? LastSyncedUtc { get; }
    IReadOnlyList<EventLogPackage> ServerPackages { get; }
    IReadOnlyList<EventLogPackage> OptionalPackages { get; }
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Local cache of the "server" event-log package catalog.
/// Until collector policy pull exists, refresh seeds from <see cref="DefaultEventLogPackages"/>.
/// </summary>
public sealed class EventLogPackageCatalogStore : IEventLogPackageCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentConfigStore _config;
    private readonly object _gate = new();
    private CatalogFile _file;

    public EventLogPackageCatalogStore(IAgentConfigStore config)
    {
        _config = config;
        _file = LoadOrSeed();
    }

    public string Source
    {
        get { lock (_gate) return string.IsNullOrWhiteSpace(_file.Source) ? "builtin" : _file.Source; }
    }

    public DateTime? LastSyncedUtc
    {
        get { lock (_gate) return _file.LastSyncedUtc; }
    }

    public IReadOnlyList<EventLogPackage> ServerPackages
    {
        get
        {
            lock (_gate)
            {
                if (_file.Packages is { Count: > 0 })
                    return _file.Packages.Select(EventLogPackageMerger.Clone).ToArray();
                return DefaultEventLogPackages.Defaults.Select(EventLogPackageMerger.Clone).ToArray();
            }
        }
    }

    public IReadOnlyList<EventLogPackage> OptionalPackages =>
        DefaultEventLogPackages.AllKnown
            .Where(p => !DefaultEventLogPackages.Defaults.Any(d =>
                string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(EventLogPackageMerger.Clone)
            .ToArray();

    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Future: GET collector /api/v1/policy/eventlog-packages
        var next = new CatalogFile
        {
            Source = "builtin",
            Version = "builtin-" + DateTime.UtcNow.ToString("yyyyMMdd"),
            LastSyncedUtc = DateTime.UtcNow,
            Packages = DefaultEventLogPackages.Defaults.Select(EventLogPackageMerger.Clone).ToList()
        };

        lock (_gate)
        {
            _file = next;
            SaveUnlocked(next);
        }

        return Task.CompletedTask;
    }

    private CatalogFile LoadOrSeed()
    {
        try
        {
            var path = CatalogPath();
            if (File.Exists(path))
            {
                var parsed = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), JsonOptions);
                if (parsed?.Packages is { Count: > 0 })
                    return parsed;
            }
        }
        catch
        {
            // fall through to seed
        }

        var seeded = new CatalogFile
        {
            Source = "builtin",
            Version = "builtin-seed",
            LastSyncedUtc = DateTime.UtcNow,
            Packages = DefaultEventLogPackages.Defaults.Select(EventLogPackageMerger.Clone).ToList()
        };
        try
        {
            SaveUnlocked(seeded);
        }
        catch
        {
            // ignore persist failure on first boot
        }

        return seeded;
    }

    private void SaveUnlocked(CatalogFile file)
    {
        var dir = _config.ResolveDataDirectory();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "server-packages.json"), JsonSerializer.Serialize(file, JsonOptions));
    }

    private string CatalogPath() =>
        Path.Combine(_config.ResolveDataDirectory(), "server-packages.json");

    private sealed class CatalogFile
    {
        public string Source { get; set; } = "builtin";
        public string? Version { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public List<EventLogPackage> Packages { get; set; } = [];
    }
}
