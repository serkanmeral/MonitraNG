using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.EventLog;

public interface IWindowsEventLogReader
{
    IReadOnlyList<IngestEventItem> ReadNew(EventLogPackage package, ChannelBookmark? bookmark, int maxEvents, out ChannelBookmark? updatedBookmark);

    /// <summary>Seed bookmark at channel end (live-only from now).</summary>
    ChannelBookmark SeedFromNow(EventLogPackage package);

    /// <summary>Seed bookmark just before the oldest event in the last <paramref name="hours"/>.</summary>
    ChannelBookmark SeedFromLastHours(EventLogPackage package, int hours);
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
            updatedBookmark = SeedFromNow(package);
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

    public ChannelBookmark SeedFromNow(EventLogPackage package)
    {
        var seedId = TryReadLatestRecordId(package) ?? 0;
        return new ChannelBookmark(
            seedId,
            DateTime.UtcNow,
            CatchUpFromNow: false,
            CursorMode: "now",
            HistoryHours: null,
            HistoryFromUtc: null);
    }

    public ChannelBookmark SeedFromLastHours(EventLogPackage package, int hours)
    {
        hours = Math.Clamp(hours, 1, 168);
        var fromUtc = DateTime.UtcNow.AddHours(-hours);
        var oldestId = TryReadOldestRecordIdInWindow(package, hours);
        if (oldestId is null)
        {
            // Nothing in window — behave like start-from-now.
            var nowBm = SeedFromNow(package);
            return nowBm with
            {
                CursorMode = "hours",
                HistoryHours = hours,
                HistoryFromUtc = fromUtc
            };
        }

        // Resume just before the oldest in-window event.
        var cursor = Math.Max(0, oldestId.Value - 1);
        return new ChannelBookmark(
            cursor,
            DateTime.UtcNow,
            CatchUpFromNow: false,
            CursorMode: "hours",
            HistoryHours: hours,
            HistoryFromUtc: fromUtc);
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

    /// <summary>Oldest matching event within the timediff window (forward read).</summary>
    private static long? TryReadOldestRecordIdInWindow(EventLogPackage package, int hours)
    {
        try
        {
            var queryText = DefaultEventLogPackages.BuildHistoryWindowQuery(package, hours);
            var query = new EventLogQuery(package.Channel, PathType.LogName, queryText)
            {
                ReverseDirection = false
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

        string? formatted = null;
        try { formatted = record.FormatDescription(); }
        catch { /* provider message DLL missing */ }

        var props = ReadPropertyStrings(record);
        string? xml = null;
        try { xml = record.ToXml(); }
        catch { /* rare provider XML failures */ }

        var payload = WindowsEventPayloadBuilder.Build(record.Id, formatted, props, xml);

        var fields = new Dictionary<string, object?>
        {
            ["channel"] = package.Channel,
            ["package"] = package.Name,
            ["eventId"] = record.Id,
            ["recordId"] = record.RecordId,
            ["provider"] = record.ProviderName
        };

        WindowsEventPayloadBuilder.ApplyToFields(fields, payload);

        var message = payload.Message;
        if (ServiceControlEventEnricher.TryEnrich(record.Id, payload.Properties, fields, out var action) &&
            !string.IsNullOrWhiteSpace(action) &&
            (string.IsNullOrWhiteSpace(formatted) || message.StartsWith("EventID ", StringComparison.Ordinal)))
        {
            message = action!;
            fields["event.action"] = action;
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
            ["message"] = message,
            ["eventData"] = payload.EventData,
            ["eventDataText"] = payload.EventDataText,
            ["properties"] = payload.Properties,
            ["xml"] = payload.Xml
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
            Message = message,
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
}
