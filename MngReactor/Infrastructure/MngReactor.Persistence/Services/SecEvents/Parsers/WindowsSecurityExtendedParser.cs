using System.Text.Json;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Windows Security extended events — account/group/AD object changes (4720, 4728, 5136…).</summary>
public sealed class WindowsSecurityExtendedParser : ISecEventParser
{
    public const string ParserIdValue = "windows.security.extended.v1";

    private static readonly HashSet<int> SupportedEventIds =
    [
        4720, 4722, 4726, 4728, 4732, 4738, 5136, 5137, 5139
    ];

    internal static bool IsExtendedEvent(JsonElement raw) =>
        raw.TryGetProperty("EventID", out var el) && IsExtendedEventId(ReadInt(el));

    private static bool IsExtendedEventId(int? eventId) =>
        eventId is not null && SupportedEventIds.Contains(eventId.Value);

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        if (raw.Raw.ValueKind != JsonValueKind.Object || !raw.Raw.TryGetProperty("EventID", out _))
            return false;

        var eventId = ReadInt(raw.Raw, "EventID");
        if (eventId is null || !IsExtendedEventId(eventId))
            return false;

        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        var type = SecEventParseHelpers.NormalizeType(raw.Source.Type);
        return product.Equals("windows", StringComparison.OrdinalIgnoreCase)
               || type.Equals("ad", StringComparison.OrdinalIgnoreCase);
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        if (raw.Raw.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Windows security extended parser requires JSON object raw payload.");

        var eventId = ReadInt(raw.Raw, "EventID");
        var timestamp = ReadTimestamp(raw.Raw, raw.ReceivedAt);
        var (action, outcome) = MapEventId(eventId);

        return new ParsedSecEvent
        {
            Timestamp = timestamp,
            EventAction = action,
            EventOutcome = outcome,
            EventCode = eventId?.ToString(),
            ActorUser = ReadString(raw.Raw, "SubjectUserName", "TargetUserName", "MemberName"),
            NetworkSrcIp = ReadString(raw.Raw, "IpAddress", "WorkstationName"),
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "ad"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, "windows"),
            SourceHost = raw.Source.Host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }

    private static (string Action, string Outcome) MapEventId(int? eventId) => eventId switch
    {
        4720 => ("account_created", "success"),
        4722 => ("account_enabled", "success"),
        4726 => ("account_deleted", "success"),
        4728 or 4732 or 4738 => ("group_member_added", "success"),
        5136 => ("directory_object_modified", "success"),
        5137 => ("directory_object_created", "success"),
        5139 => ("directory_object_deleted", "success"),
        _ => ("unknown", "unknown")
    };

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;

        return ReadInt(el);
    }

    private static int? ReadInt(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(el.GetString(), out var parsed) => parsed,
            _ => null
        };

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var el))
                continue;

            if (el.ValueKind == JsonValueKind.String)
            {
                var value = el.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return null;
    }

    private static DateTime ReadTimestamp(JsonElement root, DateTime fallback)
    {
        if (root.TryGetProperty("TimeCreated", out var el) && el.ValueKind == JsonValueKind.String)
        {
            var text = el.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed;
        }

        return fallback;
    }
}
