using System.Text.Json;
using System.Text.RegularExpressions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents.Parsers;

/// <summary>NxLog UDP JSON (IT merkezi toplayici) — Security kanali Event ID map.</summary>
public sealed partial class WindowsNxlogJsonParser : ISecEventParser
{
    public const string ParserIdValue = "windows.nxlog-json.v1";
    public const string ProductValue = "windows-nxlog";

    private static readonly HashSet<int> ExtendedEventIds =
    [
        4720, 4722, 4726, 4728, 4732, 4738, 5136, 5137, 5139
    ];

    private static readonly Regex SourceNetworkAddressRegex = SourceNetworkAddressPattern();

    private readonly ISecEventMaintenanceWindowEvaluator _maintenanceWindow;

    public WindowsNxlogJsonParser(ISecEventMaintenanceWindowEvaluator maintenanceWindow)
    {
        _maintenanceWindow = maintenanceWindow;
    }

    public string ParserId => ParserIdValue;

    public bool CanParse(SecEventRawContext raw)
    {
        var product = SecEventParseHelpers.NormalizeProduct(raw.Source.Product);
        if (product.Equals(ProductValue, StringComparison.OrdinalIgnoreCase)
            || product.Equals("windows-nxlog-json", StringComparison.OrdinalIgnoreCase))
            return TryParseRoot(raw, out var root) && IsSecurityChannel(root);

        if (!TryParseRoot(raw, out var parsed))
            return false;

        return IsSecurityChannel(parsed);
    }

    public ParsedSecEvent Parse(SecEventRawContext raw)
    {
        if (!TryParseRoot(raw, out var root))
            throw new InvalidOperationException("NxLog JSON parser requires JSON string or object payload.");

        var rawText = SecEventParseHelpers.GetRawText(raw.Raw);
        if (!IsSecurityChannel(root))
            return BuildUnknown(raw, rawText, root);

        var eventId = ReadInt(root, "EventID");
        var timestamp = ReadTimestamp(root, raw.ReceivedAt);
        var host = ReadString(root, "Hostname") ?? raw.Source.Host;
        var (action, outcome) = MapEventId(eventId, ReadInt(root, "LogonType"), timestamp);

        return new ParsedSecEvent
        {
            Timestamp = timestamp,
            EventAction = action,
            EventOutcome = outcome,
            EventCode = eventId?.ToString(),
            ActorUser = ReadString(root, "TargetUserName", "SubjectUserName", "AccountName"),
            NetworkSrcIp = ReadNetworkSrcIp(root),
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "ad"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, ProductValue),
            SourceHost = host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }

    internal static bool TryParseRoot(SecEventRawContext raw, out JsonElement root)
    {
        root = default;
        if (raw.Raw.ValueKind == JsonValueKind.Object)
        {
            root = raw.Raw;
            return true;
        }

        if (raw.Raw.ValueKind != JsonValueKind.String)
            return false;

        var text = raw.Raw.GetString();
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(text);
            root = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool IsSecurityChannel(JsonElement root)
    {
        if (root.TryGetProperty("Channel", out var channelEl))
        {
            var channel = channelEl.GetString();
            if (!string.IsNullOrWhiteSpace(channel))
            {
                if (channel.Contains("Sysmon", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (channel.Equals("Security", StringComparison.OrdinalIgnoreCase)
                    || channel.Contains("Security-Auditing", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        if (root.TryGetProperty("SourceName", out var sourceEl))
        {
            var source = sourceEl.GetString();
            if (!string.IsNullOrWhiteSpace(source)
                && source.Contains("Security-Auditing", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool LooksLikeNxlogJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.TrimStart().StartsWith('{'))
            return false;

        return raw.Contains("\"EventID\"", StringComparison.Ordinal)
               && raw.Contains("\"Hostname\"", StringComparison.Ordinal);
    }

    private ParsedSecEvent BuildUnknown(SecEventRawContext raw, string rawText, JsonElement root)
    {
        var host = ReadString(root, "Hostname") ?? raw.Source.Host;
        return new ParsedSecEvent
        {
            Timestamp = ReadTimestamp(root, raw.ReceivedAt),
            EventAction = "unknown",
            EventOutcome = "unknown",
            EventCode = ReadInt(root, "EventID")?.ToString(),
            SourceType = SecEventParseHelpers.ResolveSourceType(raw.Source, "ad"),
            SourceProduct = SecEventParseHelpers.ResolveSourceProduct(raw.Source, ProductValue),
            SourceHost = host,
            ParserId = ParserId,
            Raw = SecEventParseHelpers.ToStoredRaw(rawText),
            RawPreview = SecEventParseHelpers.ToRawPreview(rawText)
        };
    }

    private (string Action, string Outcome) MapEventId(int? eventId, int? logonType, DateTime timestamp)
    {
        if (eventId is not null && ExtendedEventIds.Contains(eventId.Value))
            return MapExtendedEventId(eventId);

        return eventId switch
        {
            4624 => MapSuccessfulLogon(logonType, timestamp),
            4625 => ("login_failed", "failure"),
            4672 => MapPrivilegedAssignment(timestamp),
            4740 => ("account_locked", "failure"),
            4771 => ("kerberos_preauth_failed", "failure"),
            _ => ("unknown", "unknown")
        };
    }

    private static (string Action, string Outcome) MapExtendedEventId(int? eventId) => eventId switch
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

    private (string Action, string Outcome) MapSuccessfulLogon(int? logonType, DateTime timestamp)
    {
        if (IsPrivilegedLogonType(logonType) && _maintenanceWindow.IsOutsideAllowedWindow(timestamp))
            return ("privileged_login_outside_window", "failure");

        return ("login_success", "success");
    }

    private (string Action, string Outcome) MapPrivilegedAssignment(DateTime timestamp) =>
        _maintenanceWindow.IsOutsideAllowedWindow(timestamp)
            ? ("privileged_login_outside_window", "failure")
            : ("privileged_assigned", "success");

    private static bool IsPrivilegedLogonType(int? logonType) =>
        logonType is 2 or 10;

    private static string? ReadNetworkSrcIp(JsonElement root)
    {
        var direct = ReadString(root, "IpAddress", "WorkstationName");
        if (!string.IsNullOrWhiteSpace(direct) && direct != "-")
            return direct;

        if (!root.TryGetProperty("Message", out var messageEl) || messageEl.ValueKind != JsonValueKind.String)
            return null;

        var match = SourceNetworkAddressRegex.Match(messageEl.GetString() ?? string.Empty);
        return match.Success ? match.Groups["ip"].Value : null;
    }

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
                if (!string.IsNullOrWhiteSpace(value) && value != "-")
                    return value;
            }
        }

        return null;
    }

    private static DateTime ReadTimestamp(JsonElement root, DateTime fallback)
    {
        if (root.TryGetProperty("EventTime", out var eventTime) && eventTime.ValueKind == JsonValueKind.String)
        {
            var text = eventTime.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed;
        }

        if (root.TryGetProperty("TimeCreated", out var created) && created.ValueKind == JsonValueKind.String)
        {
            var text = created.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed;
        }

        return fallback;
    }

    [GeneratedRegex(
        @"Source Network Address:\s*(?<ip>[^\s\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SourceNetworkAddressPattern();
}
