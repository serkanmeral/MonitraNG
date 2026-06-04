using System.Text.Json;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>Windows Security Event Log — Event ID 4624/4625/4740 (MVP: 4625).</summary>
public sealed class WindowsSecurityParser : ISecEventParser
{
    public const string ParserIdValue = "windows.security.v1";

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        var type = SecEventParseHelpers.NormalizeType(raw.Source.Type);
        if (product.Equals("windows", StringComparison.OrdinalIgnoreCase)
            || type.Equals("ad", StringComparison.OrdinalIgnoreCase))
            return true;

        return raw.Raw.ValueKind == JsonValueKind.Object
               && raw.Raw.TryGetProperty("EventID", out _);
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        if (raw.Raw.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Windows security parser requires JSON object raw payload.");

        var eventId = ReadInt(raw.Raw, "EventID");
        var timestamp = ReadTimestamp(raw.Raw, raw.ReceivedAt);
        var (action, outcome) = MapEventId(eventId);

        return new ParsedSecEvent
        {
            Timestamp = timestamp,
            EventAction = action,
            EventOutcome = outcome,
            EventCode = eventId?.ToString(),
            ActorUser = ReadString(raw.Raw, "TargetUserName", "SubjectUserName", "AccountName"),
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
        4624 => ("login_success", "success"),
        4625 => ("login_failed", "failure"),
        4740 => ("account_locked", "failure"),
        4771 => ("kerberos_preauth_failed", "failure"),
        _ => ("unknown", "unknown")
    };

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(el.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

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
