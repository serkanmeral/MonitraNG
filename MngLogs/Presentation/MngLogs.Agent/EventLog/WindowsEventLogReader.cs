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

        var fields = new Dictionary<string, object?>
        {
            ["channel"] = package.Channel,
            ["package"] = package.Name,
            ["eventId"] = record.Id,
            ["recordId"] = record.RecordId,
            ["provider"] = record.ProviderName
        };

        var props = ReadPropertyStrings(record);
        if (ServiceControlEventEnricher.TryEnrich(record.Id, props, fields, out var action) &&
            !string.IsNullOrWhiteSpace(action) &&
            string.IsNullOrWhiteSpace(message))
        {
            message = action;
        }

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
        foreach (var kv in fields)
        {
            if (!rawObj.ContainsKey(kv.Key))
                rawObj[kv.Key] = kv.Value;
        }

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
            Fields = fields
        };
    }

    private static IReadOnlyList<string?> ReadPropertyStrings(EventRecord record)
    {
        try
        {
            var props = record.Properties;
            if (props is null || props.Count == 0)
                return [];

            var list = new string?[props.Count];
            for (var i = 0; i < props.Count; i++)
                list[i] = props[i].Value?.ToString();
            return list;
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
