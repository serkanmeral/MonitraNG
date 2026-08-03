using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.EventLog;

/// <summary>
/// Local UI / CLI helper to reposition Event Log package bookmarks
/// (start from now, or last N hours of history).
/// </summary>
public sealed class EventLogCursorService(
    EventLogBookmarkStore bookmarks,
    IWindowsEventLogReader reader,
    IEventLogPackageCatalogStore catalog,
    IAgentConfigStore config)
{
    public static readonly int[] AllowedHistoryHours = [6, 24, 48, 72];

    public IReadOnlyList<EventLogCursorStatus> ListStatuses()
    {
        var policy = config.Current.Policy.EventLog;
        var packages = DefaultEventLogPackages.Resolve(policy, catalog.ServerPackages);
        var snap = bookmarks.Snapshot();
        return packages
            .Select(p =>
            {
                snap.TryGetValue(p.Name, out var bm);
                return ToStatus(p, bm);
            })
            .ToArray();
    }

    public EventLogCursorStatus Apply(string packageName, string mode, int? hours)
    {
        var package = ResolvePackage(packageName)
            ?? throw new InvalidOperationException($"Package '{packageName}' is not in the effective plan.");

        ChannelBookmark bookmark = mode.Trim().ToLowerInvariant() switch
        {
            "now" => reader.SeedFromNow(package),
            "hours" => reader.SeedFromLastHours(package, NormalizeHours(hours)),
            _ => throw new ArgumentException("mode must be 'now' or 'hours'.")
        };

        bookmarks.Set(package.Name, bookmark);
        return ToStatus(package, bookmark);
    }

    private EventLogPackage? ResolvePackage(string packageName)
    {
        var key = (packageName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(key))
            return null;

        var policy = config.Current.Policy.EventLog;
        return DefaultEventLogPackages.Resolve(policy, catalog.ServerPackages)
            .FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    private static int NormalizeHours(int? hours)
    {
        var h = hours ?? 24;
        if (!AllowedHistoryHours.Contains(h))
            throw new ArgumentException(
                $"hours must be one of: {string.Join(", ", AllowedHistoryHours)}.");
        return h;
    }

    private static EventLogCursorStatus ToStatus(EventLogPackage package, ChannelBookmark? bm) => new()
    {
        PackageName = package.Name,
        Channel = package.Channel,
        SelectionMode = package.IsAllChannel ? "all" : "selected",
        LastRecordId = bm?.LastRecordId,
        SeededAtUtc = bm?.SeededAtUtc,
        CursorMode = bm?.CursorMode,
        HistoryHours = bm?.HistoryHours,
        HistoryFromUtc = bm?.HistoryFromUtc,
        HasBookmark = bm is not null
    };
}

public sealed class EventLogCursorStatus
{
    public string PackageName { get; init; } = "";
    public string Channel { get; init; } = "";
    public string SelectionMode { get; init; } = "selected";
    public long? LastRecordId { get; init; }
    public DateTime? SeededAtUtc { get; init; }
    public string? CursorMode { get; init; }
    public int? HistoryHours { get; init; }
    public DateTime? HistoryFromUtc { get; init; }
    public bool HasBookmark { get; init; }
}

public sealed class EventLogCursorRequest
{
    public string PackageName { get; set; } = "";
    /// <summary><c>now</c> or <c>hours</c>.</summary>
    public string Mode { get; set; } = "now";
    public int? Hours { get; set; }
}
