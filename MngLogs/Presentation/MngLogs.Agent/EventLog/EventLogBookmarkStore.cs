using System.Text.Json;

namespace MngLogs.Agent.EventLog;

public sealed class EventLogBookmarkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, ChannelBookmark> _map;

    public EventLogBookmarkStore(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "eventlog-bookmarks.json");
        _map = Load();
    }

    public ChannelBookmark? Get(string packageName)
    {
        lock (_gate)
            return _map.TryGetValue(packageName, out var b) ? b with { } : null;
    }

    public void Set(string packageName, ChannelBookmark bookmark)
    {
        lock (_gate)
        {
            _map[packageName] = bookmark;
            PersistUnlocked();
        }
    }

    private Dictionary<string, ChannelBookmark> Load()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, ChannelBookmark>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(_path);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, ChannelBookmark>>(json, JsonOptions);
            return parsed is null
                ? new Dictionary<string, ChannelBookmark>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ChannelBookmark>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, ChannelBookmark>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void PersistUnlocked()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_map, JsonOptions));
        File.Move(tmp, _path, overwrite: true);
    }
}

public sealed record ChannelBookmark(
    long LastRecordId,
    DateTime? SeededAtUtc,
    bool CatchUpFromNow);
