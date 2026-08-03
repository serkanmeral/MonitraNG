using System.Text.Json;

namespace MngLogs.Agent.Linux.Journal;

public sealed class JournalBookmarkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, JournalBookmark> _map;

    public JournalBookmarkStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "journal-bookmarks.json");
        _map = Load();
    }

    public JournalBookmark? Get(string packageName)
    {
        lock (_gate)
            return _map.TryGetValue(packageName, out var b) ? b with { } : null;
    }

    public void Set(string packageName, JournalBookmark bookmark)
    {
        lock (_gate)
        {
            _map[packageName] = bookmark;
            PersistUnlocked();
        }
    }

    private Dictionary<string, JournalBookmark> Load()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, JournalBookmark>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JournalBookmark>>(
                File.ReadAllText(_path), JsonOptions);
            return parsed is null
                ? new Dictionary<string, JournalBookmark>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, JournalBookmark>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, JournalBookmark>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void PersistUnlocked()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_map, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }
}

/// <param name="Cursor">Last seen journal cursor (optional dedupe aid).</param>
/// <param name="SeededAtUtc">First-run catch-up timestamp.</param>
/// <param name="LastEventUtc">Watermark for journalctl --since (primary).</param>
public sealed record JournalBookmark(string? Cursor, DateTime? SeededAtUtc, DateTime? LastEventUtc = null);
