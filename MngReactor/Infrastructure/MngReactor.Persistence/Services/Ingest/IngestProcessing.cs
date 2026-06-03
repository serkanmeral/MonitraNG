using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Abstractions.Observations;
using MngReactor.Application.Configuration;
using MngReactor.Application.Features.Commands.Ingest;

namespace MngReactor.Persistence.Services.Ingest;

public class IngestProcessing : IIngestProcessing
{
    /// <summary>Bulk insert parca boyutu.</summary>
    private const int MaxBulkChunkSize = 1000;

    private readonly ILogger<IngestProcessing> _logger;
    private readonly IOptions<MngReactorSettings> _options;
    private readonly IMetricPublisher _metricPublisher;
    private readonly IObservationPublisher _observationPublisher;
    private readonly IIngestNotifyPublisher _ingestNotifyPublisher;
    private readonly IMonMetricsRepository _metricsRepo;
    private readonly IDataGatewayClient _dg;

    public IngestProcessing(
        ILogger<IngestProcessing> logger,
        IOptions<MngReactorSettings> options,
        IMetricPublisher metricPublisher,
        IObservationPublisher observationPublisher,
        IIngestNotifyPublisher ingestNotifyPublisher,
        IMonMetricsRepository metricsRepo,
        IDataGatewayClient dg)
    {
        _logger = logger;
        _options = options;
        _metricPublisher = metricPublisher;
        _observationPublisher = observationPublisher;
        _ingestNotifyPublisher = ingestNotifyPublisher;
        _metricsRepo = metricsRepo;
        _dg = dg;
    }

    public async Task<IngestMetricsResponse> ProcessAsync(IngestMetricsRequest request, string domainFromToken, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        var token = ResolveToken(domainFromToken, accessToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Ingest: token bulunamadı domain={Domain}", domainFromToken);
            return new IngestMetricsResponse { SavedCount = 0, FailedCount = request.Batches.Sum(b => b.Metrics.Count), ErrorList = [new IngestError { Code = "auth_error", Message = "Token required" }] };
        }

        var savedCount = 0;
        var failedCount = 0;
        var errorList = new List<IngestError>();
        var engineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bulkItems = new JsonArray();
        var observationContexts = new List<ObservationPublishContext>();

        for (var batchIndex = 0; batchIndex < request.Batches.Count; batchIndex++)
        {
            var batch = request.Batches[batchIndex];
            for (var metricIndex = 0; metricIndex < batch.Metrics.Count; metricIndex++)
            {
                var metric = batch.Metrics[metricIndex];
                var doc = BuildMetricDocument(batch, metric, domainFromToken);

                if (doc == null)
                {
                    errorList.Add(new IngestError { BatchIndex = batchIndex, MetricIndex = metricIndex, Code = "validation_error", Message = "Invalid metric data" });
                    failedCount++;
                    continue;
                }

                bulkItems.Add(doc);
                engineIds.Add(batch.EngineId);
                observationContexts.Add(new ObservationPublishContext(batch, metric, domainFromToken));

                _ = _metricPublisher.PublishAsync(ToPublishDocument(batch, metric, domainFromToken), domainFromToken, cancellationToken);
            }
        }

        if (bulkItems.Count > 0)
        {
            for (var offset = 0; offset < bulkItems.Count; offset += MaxBulkChunkSize)
            {
                var chunk = new List<JsonObject>();
                for (var i = offset; i < Math.Min(offset + MaxBulkChunkSize, bulkItems.Count); i++)
                {
                    var node = bulkItems[i];
                    if (node is JsonObject jo)
                        chunk.Add(jo);
                }

                if (chunk.Count == 0) continue;

                try
                {
                    var inserted = await _metricsRepo.InsertManyAsync(domainFromToken, chunk, cancellationToken);
                    savedCount += inserted;
                    if (inserted < chunk.Count)
                        failedCount += chunk.Count - inserted;

                    if (inserted > 0)
                    {
                        var publishCount = Math.Min(inserted, chunk.Count);
                        for (var i = 0; i < publishCount; i++)
                        {
                            var ctx = observationContexts[offset + i];
                            await PublishObservationAsync(ctx, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ingest mon_metrics bulk insert failed (chunk {Offset}-{End})", offset, offset + chunk.Count);
                    failedCount += chunk.Count;
                    errorList.Add(new IngestError { Code = "bulk_error", Message = ex.Message });
                }
            }
        }

        foreach (var engineId in engineIds)
        {
            await UpdateLastSeenAtAsync(domainFromToken, engineId, token, cancellationToken);
        }

        if (savedCount > 0 && engineIds.Count > 0)
        {
            try
            {
                await _ingestNotifyPublisher.TryPublishDataUpdatedAsync(domainFromToken, DateTime.UtcNow, engineIds.ToList(), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ingest: data.updated notify failed for domain {Domain}", domainFromToken);
            }
        }

        return new IngestMetricsResponse { SavedCount = savedCount, FailedCount = failedCount, ErrorList = errorList };
    }

    private async Task PublishObservationAsync(ObservationPublishContext ctx, CancellationToken cancellationToken)
    {
        if (!TryReadDouble(ctx.Metric.Value, out var numericValue))
            return;

        var domainName = ctx.Domain.Trim();
        var dimensions = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["assetId"] = ctx.Batch.AssetId,
            ["agentId"] = ctx.Batch.AgentId,
            ["engineId"] = ctx.Batch.EngineId
        };
        if (!string.IsNullOrEmpty(ctx.Batch.ItemId))
            dimensions["itemId"] = ctx.Batch.ItemId;
        if (!string.IsNullOrEmpty(ctx.Metric.Unit))
            dimensions["unit"] = ctx.Metric.Unit;

        await _observationPublisher.PublishAsync(
            domainName,
            domainName,
            ctx.Metric.CollectibleCode,
            numericValue,
            dimensions,
            ctx.Batch.CollectedAt,
            cancellationToken);
    }

    private static bool TryReadDouble(object? value, out double result)
    {
        result = default;
        if (value == null)
            return false;

        switch (value)
        {
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case decimal dec:
                result = (double)dec;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            case JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var jsonNum):
                result = jsonNum;
                return true;
            case JsonElement je when je.ValueKind == JsonValueKind.String &&
                                     double.TryParse(je.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var jsonParsed):
                result = jsonParsed;
                return true;
            default:
                return false;
        }
    }

    private sealed record ObservationPublishContext(IngestBatch Batch, IngestMetric Metric, string Domain);

    private static bool? GetBool(JsonNode? node, string key)
    {
        var n = node?[key];
        if (n is JsonValue jv && jv.TryGetValue(out bool b)) return b;
        return null;
    }

    private static string? GetString(JsonNode? node, string key)
    {
        var n = node?[key];
        if (n is JsonValue jv && jv.TryGetValue(out string? s)) return s;
        return n?.ToString();
    }

    private string? ResolveToken(string domain, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) return accessToken;
        return _options.Value?.DataGateway?.DomainTokens?.GetValueOrDefault(domain);
    }

    private static JsonObject? BuildMetricDocument(IngestBatch batch, IngestMetric metric, string domain)
    {
        try
        {
            var meta = new JsonObject
            {
                ["domain"] = domain,
                ["assetId"] = batch.AssetId,
                ["agentId"] = batch.AgentId,
                ["engineId"] = batch.EngineId,
                ["collectibleCode"] = metric.CollectibleCode
            };
            if (!string.IsNullOrEmpty(batch.ItemId))
                meta["itemId"] = batch.ItemId;

            var doc = new JsonObject
            {
                ["timestamp"] = batch.CollectedAt,
                ["meta"] = meta,
                ["value"] = ValueToJsonNode(metric.Value)
            };
            if (!string.IsNullOrEmpty(metric.Unit))
                doc["unit"] = metric.Unit;

            return doc;
        }
        catch
        {
            return null;
        }
    }

    private static JsonNode ValueToJsonNode(object? value)
    {
        if (value == null) return JsonValue.Create((string?)null);
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Number => JsonValue.Create(je.TryGetDouble(out var num) ? num : 0),
                JsonValueKind.String => JsonValue.Create(je.GetString() ?? ""),
                JsonValueKind.True => JsonValue.Create(true),
                JsonValueKind.False => JsonValue.Create(false),
                JsonValueKind.Object => JsonNode.Parse(je.GetRawText()) ?? JsonValue.Create((string?)null),
                JsonValueKind.Array => JsonNode.Parse(je.GetRawText()) ?? new JsonArray(),
                JsonValueKind.Null => JsonValue.Create((string?)null),
                _ => JsonValue.Create(je.GetRawText())
            };
        }
        if (value is int i) return JsonValue.Create(i);
        if (value is long l) return JsonValue.Create(l);
        if (value is double dbl) return JsonValue.Create(dbl);
        if (value is float f) return JsonValue.Create(f);
        if (value is string s) return JsonValue.Create(s);
        if (value is bool b) return JsonValue.Create(b);
        if (value is DateTime dt) return JsonValue.Create(dt);
        return JsonValue.Create(value.ToString() ?? "");
    }

    private static object ToPublishDocument(IngestBatch batch, IngestMetric metric, string domain)
    {
        return new
        {
            domain,
            assetId = batch.AssetId,
            itemId = batch.ItemId,
            agentId = batch.AgentId,
            engineId = batch.EngineId,
            collectibleCode = metric.CollectibleCode,
            value = metric.Value,
            unit = metric.Unit,
            timestamp = batch.CollectedAt
        };
    }

    private async Task UpdateLastSeenAtAsync(string domain, string engineId, string token, CancellationToken cancellationToken)
    {
        try
        {
            var data = new JsonObject { ["lastSeenAt"] = DateTime.UtcNow };
            await _dg.UpdateAsync("mon_engines", engineId, data, token, cancellationToken, skipEventPublish: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not update lastSeenAt for engine {EngineId} in domain {Domain}", engineId, domain);
        }
    }
}
