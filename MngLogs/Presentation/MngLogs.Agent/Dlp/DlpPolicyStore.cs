using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Transport;

namespace MngLogs.Agent.Dlp;

public interface IDlpPolicyStore
{
    DlpCompiledPolicy Current { get; }
    string Source { get; }
    DateTime? LastSyncedUtc { get; }
    Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default);
}

public sealed class DlpPolicyStore : IDlpPolicyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAgentConfigStore _config;
    private readonly ICollectorClient _collector;
    private readonly object _gate = new();
    private CatalogFile _file;

    public DlpPolicyStore(IAgentConfigStore config, ICollectorClient collector)
    {
        _config = config;
        _collector = collector;
        _file = LoadOrSeed();
    }

    public DlpCompiledPolicy Current
    {
        get
        {
            lock (_gate)
            {
                return Clone(_file.Policy);
            }
        }
    }

    public string Source
    {
        get { lock (_gate) return string.IsNullOrWhiteSpace(_file.Source) ? "builtin" : _file.Source; }
    }

    public DateTime? LastSyncedUtc
    {
        get { lock (_gate) return _file.LastSyncedUtc; }
    }

    public async Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        string? ifNoneMatch = null;
        if (!force)
        {
            lock (_gate)
            {
                if (!string.IsNullOrWhiteSpace(_file.Policy.Version) &&
                    !string.Equals(_file.Source, "builtin", StringComparison.OrdinalIgnoreCase))
                {
                    ifNoneMatch = _file.Policy.Version;
                }
            }
        }

        var pull = await _collector.GetDlpPolicyAsync(ifNoneMatch, cancellationToken);
        if (pull.NotModified)
        {
            lock (_gate)
            {
                _file.LastSyncedUtc = DateTime.UtcNow;
                try { SaveUnlocked(_file); } catch { /* ignore */ }
            }

            return;
        }

        if (!pull.Success || pull.Policy is null)
            return;

        lock (_gate)
        {
            _file = new CatalogFile
            {
                Source = "collector",
                LastSyncedUtc = DateTime.UtcNow,
                Policy = pull.Policy
            };
            try { SaveUnlocked(_file); } catch { /* ignore */ }
        }
    }

    private CatalogFile LoadOrSeed()
    {
        var path = CachePath();
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), JsonOptions);
                if (loaded?.Policy is not null)
                    return loaded;
            }
        }
        catch
        {
            /* seed */
        }

        return new CatalogFile
        {
            Source = "builtin",
            Policy = BuiltinFailOpen()
        };
    }

    private void SaveUnlocked(CatalogFile file)
    {
        var path = CachePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    private string CachePath() => Path.Combine(_config.ResolveDataDirectory(), "dlp-policy.json");

    private static DlpCompiledPolicy Clone(DlpCompiledPolicy src) =>
        JsonSerializer.Deserialize<DlpCompiledPolicy>(JsonSerializer.Serialize(src, JsonOptions), JsonOptions)
        ?? BuiltinFailOpen();

    public static DlpCompiledPolicy BuiltinFailOpen() => new()
    {
        SchemaVersion = 1,
        PolicyId = "builtin",
        Version = "0",
        EnforcementMode = "auditOnly",
        Unclassified = new DlpUnclassified { Allow = true, Effect = "audit" }
    };

    private sealed class CatalogFile
    {
        public string Source { get; set; } = "builtin";
        public DateTime? LastSyncedUtc { get; set; }
        public DlpCompiledPolicy Policy { get; set; } = BuiltinFailOpen();
    }
}
