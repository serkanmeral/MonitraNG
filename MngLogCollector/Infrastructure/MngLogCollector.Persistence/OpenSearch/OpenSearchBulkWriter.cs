using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.OpenSearch;
using MngLogCollector.Application.Configuration;

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
        var action = string.IsNullOrWhiteSpace(doc.Message) ? sourceType : doc.Message;

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
                ["code"] = ExtractEventCode(doc.Fields)
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

    private static string JsonEscape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
