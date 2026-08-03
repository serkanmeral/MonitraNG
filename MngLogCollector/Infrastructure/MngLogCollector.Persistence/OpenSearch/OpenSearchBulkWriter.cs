using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.OpenSearch;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Services.Ingest;

namespace MngLogCollector.Persistence.OpenSearch;

public sealed class OpenSearchBulkWriter : IOpenSearchBulkWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngLogCollectorSettings _settings;
    private readonly ILogger<OpenSearchBulkWriter> _logger;

    public OpenSearchBulkWriter(
        IHttpClientFactory httpClientFactory,
        IOptions<MngLogCollectorSettings> options,
        ILogger<OpenSearchBulkWriter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<int> IndexSecEventsAsync(
        string domain,
        IReadOnlyList<OpenSearchSecEventDocument> documents,
        CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
            return 0;

        var baseUrl = (_settings.OpenSearch.Url ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("OpenSearch write skipped: Url is empty");
            return 0;
        }

        try
        {
            var body = BuildBulkNdjson(domain, documents);
            var client = _httpClientFactory.CreateClient("opensearch");
            using var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");

            using var response = await client.PostAsync($"{baseUrl}/_bulk", content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenSearch bulk failed status={Status} domain={Domain} count={Count} body={Body}",
                    (int)response.StatusCode,
                    domain,
                    documents.Count,
                    Truncate(responseBody, 500));
                return 0;
            }

            if (responseBody.Contains("\"errors\":true", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "OpenSearch bulk partial errors domain={Domain} count={Count} body={Body}",
                    domain,
                    documents.Count,
                    Truncate(responseBody, 500));
                return 0;
            }

            _logger.LogDebug(
                "OpenSearch bulk ok domain={Domain} count={Count}",
                domain,
                documents.Count);

            return documents.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "OpenSearch bulk failed domain={Domain} count={Count}",
                domain,
                documents.Count);
            return 0;
        }
    }

    internal static string BuildBulkNdjson(string domain, IReadOnlyList<OpenSearchSecEventDocument> documents)
    {
        var sb = new StringBuilder(documents.Count * 512);
        foreach (var doc in documents)
        {
            var indexName = OpenSearchIndexNames.BuildDailySecEventsIndexName(domain, doc.IngestedAtUtc);
            sb.Append("{\"index\":{\"_index\":\"").Append(indexName)
                .Append("\",\"_id\":\"").Append(JsonEscape(doc.Id)).Append("\"}}").Append('\n');
            sb.Append(JsonSerializer.Serialize(ToPayload(domain, doc), JsonOptions)).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Align with Reactor SecEvent OpenSearch shape so shared daily indices do not mapper-conflict.
    /// </summary>
    internal static Dictionary<string, object?> ToPayload(string domain, OpenSearchSecEventDocument doc)
    {
        var eventTime = doc.EventTimeUtc.Kind == DateTimeKind.Utc ? doc.EventTimeUtc : doc.EventTimeUtc.ToUniversalTime();
        var ingestedAt = doc.IngestedAtUtc.Kind == DateTimeKind.Utc ? doc.IngestedAtUtc : doc.IngestedAtUtc.ToUniversalTime();
        var hostName = string.IsNullOrWhiteSpace(doc.Hostname) ? doc.HostId : doc.Hostname;
        var sourceType = string.IsNullOrWhiteSpace(doc.Source) ? "endpoint" : doc.Source;
        var actionFromFields = ExtractEventAction(doc.Fields);
        var eventCode = ExtractEventCode(doc.Fields);
        var normalized = AgentSecEventActionNormalizer.TryNormalize(
            doc.SourceProduct,
            doc.Source,
            eventCode,
            doc.Message ?? doc.RawPreview);
        var action = !string.IsNullOrWhiteSpace(normalized)
            ? normalized!
            : !string.IsNullOrWhiteSpace(actionFromFields)
                ? actionFromFields!
                : (string.IsNullOrWhiteSpace(doc.Message) ? sourceType : doc.Message);

        var payload = new Dictionary<string, object?>
        {
            ["@timestamp"] = eventTime,
            ["ingestedAt"] = ingestedAt,
            ["domain"] = domain,
            ["source"] = new Dictionary<string, object?>
            {
                ["type"] = sourceType,
                ["product"] = string.IsNullOrWhiteSpace(doc.SourceProduct) ? "mnglogs-agent" : doc.SourceProduct,
                ["host"] = hostName
            },
            ["event"] = new Dictionary<string, object?>
            {
                ["action"] = action,
                ["outcome"] = MapOutcome(doc.Severity),
                ["code"] = eventCode
            },
            ["parser"] = new Dictionary<string, object?>
            {
                ["id"] = "mnglogcollector"
            },
            ["rawPreview"] = doc.RawPreview,
            ["host"] = new Dictionary<string, object?>
            {
                ["name"] = hostName,
                ["id"] = doc.HostId
            },
            ["agent"] = new Dictionary<string, object?>
            {
                ["type"] = "mnglogs",
                ["id"] = doc.HostId
            }
        };

        TryAddRdpActorAndNetwork(payload, doc.Fields);

        if (doc.Fields is { Count: > 0 })
            payload["fields"] = doc.Fields;

        if (!string.IsNullOrWhiteSpace(doc.Severity))
            payload["severity"] = doc.Severity;

        return payload;
    }

    private static string? MapOutcome(string? severity) =>
        severity?.ToLowerInvariant() switch
        {
            "error" or "critical" => "failure",
            "warning" => "unknown",
            _ => "success"
        };

    private static string? ExtractEventAction(Dictionary<string, object?>? fields)
    {
        if (fields is null)
            return null;
        if (fields.TryGetValue("event.action", out var a) && a is not null)
        {
            var s = a.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }
        return null;
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

    private static void TryAddRdpActorAndNetwork(
        Dictionary<string, object?> payload,
        Dictionary<string, object?>? fields)
    {
        if (fields is null || fields.Count == 0)
            return;

        var user = ReadField(fields, "User", "eventData.User", "TargetUserName");
        var address = ReadField(fields, "Address", "eventData.Address", "SourceNetworkAddress");

        if (!string.IsNullOrWhiteSpace(user))
        {
            payload["actor"] = new Dictionary<string, object?> { ["user"] = user.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(address)
            && !address.Equals("-", StringComparison.Ordinal)
            && !address.Equals("LOCAL", StringComparison.OrdinalIgnoreCase))
        {
            payload["network"] = new Dictionary<string, object?> { ["srcIp"] = address.Trim() };
        }
    }

    private static string? ReadField(Dictionary<string, object?> fields, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!fields.TryGetValue(key, out var value) || value is null)
                continue;
            if (value is JsonElement el)
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
                continue;
            }

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        // Nested eventData dictionary from agent payload.
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
                        var s = v.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s;
                    }
                }
            }
        }

        return null;
    }

    private static string JsonEscape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
