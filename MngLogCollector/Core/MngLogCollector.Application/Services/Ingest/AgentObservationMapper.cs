using System.Globalization;
using System.Text.Json;
using MngLogCollector.Application.Abstractions.Observations;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Ingest;

namespace MngLogCollector.Application.Services.Ingest;

/// <summary>
/// Maps agent ingest events to MngAlarm observation envelopes
/// (aligned with Reactor SecEventObservationMapper / SEC_EVENT_OBSERVATION_MAP).
/// </summary>
public static class AgentObservationMapper
{
    public const string ExchangeName = "monitra.observations";

    public static string BuildEventRoutingKey(string domainId, string eventKey) =>
        $"{domainId.Trim()}.event.{eventKey.Trim()}";

    public static bool IsSourceProductAllowed(
        ObservationPublishSettings settings,
        string? sourceProduct)
    {
        if (!settings.Enabled)
            return false;

        var product = (sourceProduct ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(product))
            return false;

        // Empty allowlist or explicit "*" → publish every stamped package.
        if (settings.SourceProducts.Count == 0
            || settings.SourceProducts.Any(p =>
                !string.IsNullOrWhiteSpace(p)
                && p.Trim().Equals("*", StringComparison.Ordinal)))
            return true;

        return settings.SourceProducts.Any(p =>
            !string.IsNullOrWhiteSpace(p)
            && p.Trim().Equals(product, StringComparison.OrdinalIgnoreCase));
    }

    public static AgentObservationPayload? TryMap(
        string domain,
        string hostId,
        string? hostname,
        IngestEventItem item)
    {
        var domainName = domain.Trim();
        if (string.IsNullOrWhiteSpace(domainName))
            return null;

        var eventCode = ExtractEventCode(item.Fields);
        var messageFallback = item.Message ?? BuildRawPreview(item.Raw);
        var key = AgentSecEventActionNormalizer.ResolveObservationKey(
            item.SourceProduct,
            item.Source,
            eventCode,
            messageFallback);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var dimensions = new Dictionary<string, object?>(StringComparer.Ordinal);
        var user = ExtractRdpUser(item.Fields);
        var srcIp = ExtractRdpAddress(item.Fields);
        if (!string.IsNullOrWhiteSpace(user))
            dimensions["userId"] = user.Trim();
        if (!string.IsNullOrWhiteSpace(srcIp))
            dimensions["srcIp"] = srcIp.Trim();

        var sourceType = string.IsNullOrWhiteSpace(item.Source) ? "windows-eventlog" : item.Source.Trim();
        dimensions["sourceType"] = sourceType;
        dimensions["sourceHost"] = ShortHostName(hostname, hostId);
        dimensions["parserId"] = "mnglogcollector";
        if (!string.IsNullOrWhiteSpace(item.SourceProduct))
            dimensions["sourceProduct"] = item.SourceProduct.Trim();
        if (!string.IsNullOrWhiteSpace(eventCode))
            dimensions["eventCode"] = eventCode.Trim();
        if (!string.IsNullOrWhiteSpace(item.Id))
            dimensions["secEventId"] = item.Id.Trim();

        var timestamp = item.TimestampUtc?.ToUniversalTime() ?? DateTime.UtcNow;

        return new AgentObservationPayload
        {
            DomainId = domainName,
            DomainName = domainName,
            Key = key!,
            Value = 1,
            Timestamp = timestamp,
            Dimensions = dimensions
        };
    }

    public static string SerializeEventPayload(AgentObservationPayload payload)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["domainName"] = payload.DomainName.Trim(),
            ["domainId"] = payload.DomainId.Trim(),
            ["kind"] = "event",
            ["key"] = payload.Key.Trim(),
            ["value"] = payload.Value,
            ["timestamp"] = payload.Timestamp.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture),
            ["dimensions"] = new Dictionary<string, object?>(payload.Dimensions, StringComparer.Ordinal)
        };
        return JsonSerializer.Serialize(body);
    }

    public static string ShortHostName(string? hostname, string hostId)
    {
        var raw = !string.IsNullOrWhiteSpace(hostname) ? hostname.Trim() : hostId.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "_missing";
        var dot = raw.IndexOf('.');
        return dot > 0 ? raw[..dot] : raw;
    }

    private static string? ExtractEventCode(Dictionary<string, object?>? fields)
    {
        if (fields is null)
            return null;
        if (fields.TryGetValue("eventId", out var id) && id is not null)
            return id.ToString();
        if (fields.TryGetValue("EventID", out var id2) && id2 is not null)
            return id2.ToString();
        return null;
    }

    private static string? ExtractRdpUser(Dictionary<string, object?>? fields) =>
        ReadField(fields, "User", "eventData.User", "TargetUserName");

    private static string? ExtractRdpAddress(Dictionary<string, object?>? fields)
    {
        var address = ReadField(fields, "Address", "eventData.Address", "SourceNetworkAddress");
        if (string.IsNullOrWhiteSpace(address)
            || address.Equals("-", StringComparison.Ordinal)
            || address.Equals("LOCAL", StringComparison.OrdinalIgnoreCase))
            return null;
        return address;
    }

    private static string? ReadField(Dictionary<string, object?>? fields, params string[] keys)
    {
        if (fields is null || fields.Count == 0)
            return null;

        foreach (var key in keys)
        {
            if (!fields.TryGetValue(key, out var value) || value is null)
                continue;
            var text = ValueAsString(value);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        if (fields.TryGetValue("eventData", out var ed) && ed is not null)
        {
            if (ed is JsonElement jel && jel.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in keys)
                {
                    var shortKey = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
                    if (jel.TryGetProperty(shortKey, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s;
                    }
                }
            }
            else if (ed is Dictionary<string, object?> dict)
            {
                foreach (var key in keys)
                {
                    var shortKey = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
                    if (dict.TryGetValue(shortKey, out var v) && v is not null)
                    {
                        var text = ValueAsString(v);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }
            else if (ed is IDictionary<string, object?> idict)
            {
                foreach (var key in keys)
                {
                    var shortKey = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
                    if (idict.TryGetValue(shortKey, out var v) && v is not null)
                    {
                        var text = ValueAsString(v);
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;
                    }
                }
            }
        }

        return null;
    }

    private static string? ValueAsString(object value)
    {
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.ToString(),
                _ => null
            };
        }

        return value.ToString();
    }

    private static string? BuildRawPreview(JsonElement? raw)
    {
        if (raw is null)
            return null;
        return raw.Value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => raw.Value.GetString(),
            _ => raw.Value.GetRawText()
        };
    }
}
