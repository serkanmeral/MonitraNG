using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Transport;

namespace MngLogs.Agent.EventLog;

public interface IEventLogPackageCatalogStore
{
    /// <summary>builtin | collector | cache.</summary>
    string Source { get; }
    DateTime? LastSyncedUtc { get; }
    string? Version { get; }
    IReadOnlyList<EventLogPackage> ServerPackages { get; }
    IReadOnlyList<EventLogPackage> OptionalPackages { get; }
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Local cache of the server event-log package catalog.
/// Pulls from collector <c>GET /api/v1/policy/eventlog-packages</c>; falls back to builtin on failure.
/// </summary>
public sealed class EventLogPackageCatalogStore : IEventLogPackageCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentConfigStore _config;
    private readonly ICollectorClient _collector;
    private readonly object _gate = new();
    private CatalogFile _file;

    public EventLogPackageCatalogStore(IAgentConfigStore config, ICollectorClient collector)
    {
        _config = config;
        _collector = collector;
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

    public string? Version
    {
        get { lock (_gate) return _file.Version; }
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

    public IReadOnlyList<EventLogPackage> OptionalPackages
    {
        get
        {
            lock (_gate)
            {
                if (_file.OptionalPackages is { Count: > 0 })
                    return _file.OptionalPackages.Select(EventLogPackageMerger.Clone).ToArray();
            }

            return DefaultEventLogPackages.AllKnown
                .Where(p => !DefaultEventLogPackages.Defaults.Any(d =>
                    string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(EventLogPackageMerger.Clone)
                .ToArray();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Only send If-None-Match when we already trust a collector-sourced cache.
        string? ifNoneMatch = null;
        lock (_gate)
        {
            if (!string.IsNullOrWhiteSpace(_file.Version) &&
                !string.Equals(_file.Source, "builtin", StringComparison.OrdinalIgnoreCase))
            {
                ifNoneMatch = _file.Version;
            }
        }

        var pull = await _collector.GetEventLogPackageCatalogAsync(ifNoneMatch, cancellationToken);
        CatalogFile next;

        if (pull.NotModified)
        {
            lock (_gate)
            {
                _file.LastSyncedUtc = DateTime.UtcNow;
                try { SaveUnlocked(_file); } catch { /* ignore */ }
            }
            return;
        }

        if (pull is { Success: true, Catalog: { Packages.Count: > 0 } remote })
        {
            next = new CatalogFile
            {
                Source = string.IsNullOrWhiteSpace(remote.Source) ? "collector" : remote.Source.Trim(),
                Version = string.IsNullOrWhiteSpace(remote.Version) ? remote.GeneratedUtc.ToString("o") : remote.Version,
                LastSyncedUtc = DateTime.UtcNow,
                Packages = MapPackages(remote.Packages),
                OptionalPackages = MapPackages(remote.OptionalPackages)
            };
        }
        else
        {
            // Collector unreachable or empty → keep last good cache if present; else builtin seed.
            lock (_gate)
            {
                if (_file.Packages is { Count: > 0 } &&
                    !string.Equals(_file.Source, "builtin", StringComparison.OrdinalIgnoreCase))
                {
                    _file.LastSyncedUtc = DateTime.UtcNow;
                    try { SaveUnlocked(_file); } catch { /* ignore */ }
                    return;
                }
            }

            next = new CatalogFile
            {
                Source = "builtin",
                Version = "builtin-" + DateTime.UtcNow.ToString("yyyyMMdd"),
                LastSyncedUtc = DateTime.UtcNow,
                Packages = DefaultEventLogPackages.Defaults.Select(EventLogPackageMerger.Clone).ToList(),
                OptionalPackages = DefaultEventLogPackages.AllKnown
                    .Where(p => !DefaultEventLogPackages.Defaults.Any(d =>
                        string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(EventLogPackageMerger.Clone)
                    .ToList()
            };
        }

        lock (_gate)
        {
            _file = next;
            SaveUnlocked(next);
        }
    }

    private static List<EventLogPackage> MapPackages(IEnumerable<EventLogPackageCatalogItem>? items)
    {
        if (items is null)
            return [];

        return items
            .Select(p => new EventLogPackage
            {
                Name = p.Name?.Trim() ?? "",
                Channel = p.Channel?.Trim() ?? "",
                EventIds = p.EventIds?.Distinct().OrderBy(x => x).ToList() ?? []
            })
            .Where(EventLogPackageMerger.IsValid)
            .Select(EventLogPackageMerger.Clone)
            .ToList();
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
            Packages = DefaultEventLogPackages.Defaults.Select(EventLogPackageMerger.Clone).ToList(),
            OptionalPackages = DefaultEventLogPackages.AllKnown
                .Where(p => !DefaultEventLogPackages.Defaults.Any(d =>
                    string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(EventLogPackageMerger.Clone)
                .ToList()
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
        public List<EventLogPackage> OptionalPackages { get; set; } = [];
    }
}
