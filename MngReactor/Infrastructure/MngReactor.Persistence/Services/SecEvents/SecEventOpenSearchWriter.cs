using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Models.SecEvents;
using System.Net.Http.Headers;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventOpenSearchWriter : ISecEventOpenSearchWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SecEventOpenSearchWriter> _logger;
    private readonly SecEventsSettings _settings;

    public SecEventOpenSearchWriter(
        IHttpClientFactory httpClientFactory,
        ILogger<SecEventOpenSearchWriter> logger,
        IOptions<MngReactorSettings> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = options?.Value?.SecEvents ?? new SecEventsSettings();
    }

    public Task IndexManyAsync(
        string domain,
        IReadOnlyList<(string Id, SecEventDocument Document)> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return Task.CompletedTask;

        var mapped = items
            .Select(i => new SecEventOpenSearchIndexItem(i.Id, i.Document))
            .ToList();
        return IndexManyCoreAsync(domain, mapped, cancellationToken);
    }

    private async Task IndexManyCoreAsync(
        string domain,
        IReadOnlyList<SecEventOpenSearchIndexItem> items,
        CancellationToken cancellationToken)
    {
        if (!_settings.OpenSearchDualWriteEnabled || items.Count == 0)
            return;

        var baseUrl = (_settings.OpenSearchUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("sec_events OpenSearch dual-write enabled but OpenSearchUrl is empty");
            return;
        }

        try
        {
            var body = BuildBulkNdjson(domain, items);
            var client = _httpClientFactory.CreateClient("opensearch");
            using var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ndjson");

            using var response = await client.PostAsync($"{baseUrl}/_bulk", content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "sec_events OpenSearch bulk failed status={Status} domain={Domain} count={Count} body={Body}",
                    (int)response.StatusCode,
                    domain,
                    items.Count,
                    Truncate(responseBody, 500));
                return;
            }

            if (responseBody.Contains("\"errors\":true", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "sec_events OpenSearch bulk partial errors domain={Domain} count={Count} body={Body}",
                    domain,
                    items.Count,
                    Truncate(responseBody, 500));
                return;
            }

            _logger.LogDebug(
                "sec_events OpenSearch dual-write ok domain={Domain} count={Count}",
                domain,
                items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "sec_events OpenSearch dual-write failed domain={Domain} count={Count}",
                domain,
                items.Count);
        }
    }

    internal static string BuildBulkNdjson(string domain, IReadOnlyList<SecEventOpenSearchIndexItem> items)
    {
        var sb = new StringBuilder(items.Count * 512);
        foreach (var item in items)
        {
            var indexName = SecEventOpenSearchIndexNames.BuildDailyIndexName(domain, item.Document.IngestedAt);
            sb.Append("{\"index\":{\"_index\":\"").Append(indexName)
                .Append("\",\"_id\":\"").Append(JsonEscape(item.Id)).Append("\"}}").Append('\n');
            sb.Append(JsonSerializer.Serialize(ToPayload(item.Document), JsonOptions)).Append('\n');
        }

        return sb.ToString();
    }

    // Back-compat for unit tests that pass documents only
    internal static string BuildBulkNdjson(string domain, IReadOnlyList<SecEventDocument> documents) =>
        BuildBulkNdjson(
            domain,
            documents.Select(d => new SecEventOpenSearchIndexItem("testid", d)).ToList());

    internal static string BuildIndexName(string domain, DateTime ingestedAtUtc) =>
        SecEventOpenSearchIndexNames.BuildDailyIndexName(domain, ingestedAtUtc);

    internal static string SanitizeDomain(string domain) =>
        SecEventOpenSearchIndexNames.SanitizeDomain(domain);

    private static Dictionary<string, object?> ToPayload(SecEventDocument doc)
    {
        var timestamp = doc.Timestamp.Kind == DateTimeKind.Utc ? doc.Timestamp : doc.Timestamp.ToUniversalTime();
        var ingestedAt = doc.IngestedAt.Kind == DateTimeKind.Utc ? doc.IngestedAt : doc.IngestedAt.ToUniversalTime();

        var payload = new Dictionary<string, object?>
        {
            ["@timestamp"] = timestamp,
            ["ingestedAt"] = ingestedAt,
            ["domain"] = doc.Domain,
            ["source"] = new Dictionary<string, object?>
            {
                ["type"] = NullIfEmpty(doc.Source.Type),
                ["product"] = NullIfEmpty(doc.Source.Product),
                ["host"] = NullIfEmpty(doc.Source.Host)
            },
            ["event"] = new Dictionary<string, object?>
            {
                ["action"] = doc.Event.Action,
                ["outcome"] = NullIfEmpty(doc.Event.Outcome),
                ["code"] = NullIfEmpty(doc.Event.Code)
            },
            ["parser"] = new Dictionary<string, object?> { ["id"] = doc.Parser.Id },
            ["rawPreview"] = doc.RawPreview
        };

        if (!string.IsNullOrEmpty(doc.Source.Host))
            payload["host"] = new Dictionary<string, object?> { ["name"] = doc.Source.Host };

        if (!string.IsNullOrEmpty(doc.Raw))
            payload["raw"] = doc.Raw;

        if (doc.BaselineNewFlowPair)
            payload["baseline"] = new Dictionary<string, object?> { ["newFlowPair"] = true };

        if (doc.Actor?.User is not null)
            payload["actor"] = new Dictionary<string, object?> { ["user"] = doc.Actor.User };

        if (doc.Network is not null)
        {
            var network = new Dictionary<string, object?>();
            if (doc.Network.SrcIp is not null) network["srcIp"] = doc.Network.SrcIp;
            if (doc.Network.DstIp is not null) network["dstIp"] = doc.Network.DstIp;
            if (doc.Network.DstPort is not null) network["dstPort"] = doc.Network.DstPort.Value;
            if (doc.Network.Protocol is not null) network["protocol"] = doc.Network.Protocol;
            if (network.Count > 0)
                payload["network"] = network;
        }

        return payload;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string JsonEscape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
