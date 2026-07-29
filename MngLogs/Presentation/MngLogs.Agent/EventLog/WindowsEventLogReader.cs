using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.EventLog;

public interface IWindowsEventLogReader
{
    IReadOnlyList<IngestEventItem> ReadNew(EventLogPackage package, ChannelBookmark? bookmark, int maxEvents, out ChannelBookmark? updatedBookmark);
}

/// <summary>Windows-only Event Log reader. First run seeds "now" (no historical flood).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsEventLogReader : IWindowsEventLogReader
{
    public IReadOnlyList<IngestEventItem> ReadNew(
        EventLogPackage package,
        ChannelBookmark? bookmark,
        int maxEvents,
        out ChannelBookmark? updatedBookmark)
    {
        updatedBookmark = bookmark;
        maxEvents = Math.Max(1, maxEvents);

        if (!OperatingSystem.IsWindows())
            return [];

        // First run: seed at current end so we only ship events after agent start.
        if (bookmark is null || bookmark.CatchUpFromNow)
        {
            var seedId = TryReadLatestRecordId(package);
            updatedBookmark = new ChannelBookmark(seedId ?? 0, DateTime.UtcNow, CatchUpFromNow: false);
            return [];
        }

        var queryText = DefaultEventLogPackages.BuildQuery(package, bookmark.LastRecordId);
        var query = new EventLogQuery(package.Channel, PathType.LogName, queryText)
        {
            ReverseDirection = false
        };

        var items = new List<IngestEventItem>();
        long lastId = bookmark.LastRecordId;

        try
        {
            using var reader = new EventLogReader(query);
            for (var i = 0; i < maxEvents; i++)
            {
                using var record = reader.ReadEvent();
                if (record is null)
                    break;

                var recordId = record.RecordId ?? 0;
                if (recordId > lastId)
                    lastId = recordId;

                items.Add(Map(package, record));
            }
        }
        catch (EventLogNotFoundException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }

        if (lastId != bookmark.LastRecordId || items.Count > 0)
            updatedBookmark = bookmark with { LastRecordId = lastId, CatchUpFromNow = false };

        return items;
    }

    private static long? TryReadLatestRecordId(EventLogPackage package)
    {
        try
        {
            var query = new EventLogQuery(package.Channel, PathType.LogName, "*")
            {
                ReverseDirection = true
            };
            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            return record?.RecordId;
        }
        catch
        {
            return null;
        }
    }

    private static IngestEventItem Map(EventLogPackage package, EventRecord record)
    {
        var level = record.Level switch
        {
            1 => "critical",
            2 => "error",
            3 => "warning",
            4 => "info",
            5 => "verbose",
            _ => "info"
        };

        string? message = null;
        try { message = record.FormatDescription(); }
        catch { /* provider message DLL missing */ }

        var rawObj = new Dictionary<string, object?>
        {
            ["channel"] = package.Channel,
            ["package"] = package.Name,
            ["eventId"] = record.Id,
            ["recordId"] = record.RecordId,
            ["provider"] = record.ProviderName,
            ["level"] = record.Level,
            ["timeCreated"] = record.TimeCreated,
            ["machine"] = record.MachineName,
            ["message"] = message
        };

        var rawJson = JsonSerializer.SerializeToElement(rawObj);

        return new IngestEventItem
        {
            Id = $"{package.Channel}:{record.RecordId ?? 0}:{record.Id}",
            TimestampUtc = record.TimeCreated?.ToUniversalTime(),
            Source = "windows-eventlog",
            SourceProduct = package.Name,
            Severity = level,
            Message = Truncate(message ?? $"EventID {record.Id}", 512),
            Raw = rawJson,
            Fields = new Dictionary<string, object?>
            {
                ["channel"] = package.Channel,
                ["package"] = package.Name,
                ["eventId"] = record.Id,
                ["recordId"] = record.RecordId,
                ["provider"] = record.ProviderName
            }
        };
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
