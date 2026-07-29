using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Ingest;
using MngLogCollector.Application.Abstractions.OpenSearch;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Ingest;

namespace MngLogCollector.Application.Services.Ingest;

/// <summary>High-volume path: direct service (no MediatR).</summary>
public sealed class IngestBatchService : IIngestBatchService
{
    private readonly IOpenSearchBulkWriter _writer;
    private readonly MngLogCollectorSettings _settings;
    private readonly ILogger<IngestBatchService> _logger;

    public IngestBatchService(
        IOpenSearchBulkWriter writer,
        IOptions<MngLogCollectorSettings> options,
        ILogger<IngestBatchService> logger)
    {
        _writer = writer;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<IngestBatchResponse> IngestAsync(
        IngestBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var domain = (request.Domain ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain is required.", nameof(request));

        var hostId = (request.HostId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(hostId))
            throw new ArgumentException("HostId is required.", nameof(request));

        var events = request.Events ?? [];
        var max = Math.Max(1, _settings.Ingest.MaxEventsPerBatch);
        if (events.Count > max)
            throw new ArgumentException($"Batch exceeds MaxEventsPerBatch ({max}).", nameof(request));

        var now = DateTime.UtcNow;
        var docs = new List<OpenSearchSecEventDocument>(events.Count);
        foreach (var e in events)
        {
            var id = string.IsNullOrWhiteSpace(e.Id) ? Guid.NewGuid().ToString("N") : e.Id.Trim();
            var eventTime = e.TimestampUtc?.ToUniversalTime() ?? now;
            docs.Add(new OpenSearchSecEventDocument
            {
                Id = id,
                IngestedAtUtc = now,
                EventTimeUtc = eventTime,
                HostId = hostId,
                Hostname = request.Hostname,
                Source = string.IsNullOrWhiteSpace(e.Source) ? "unknown" : e.Source.Trim(),
                SourceProduct = e.SourceProduct,
                Severity = e.Severity,
                Message = e.Message,
                RawPreview = BuildRawPreview(e.Raw),
                Fields = e.Fields
            });
        }

        var written = 0;
        if (_settings.OpenSearch.WriteEnabled && docs.Count > 0)
        {
            written = await _writer.IndexSecEventsAsync(domain, docs, cancellationToken);
        }
        else if (!_settings.OpenSearch.WriteEnabled)
        {
            _logger.LogDebug(
                "Ingest accepted without OpenSearch write domain={Domain} host={HostId} count={Count}",
                domain,
                hostId,
                docs.Count);
        }

        return new IngestBatchResponse
        {
            Accepted = docs.Count,
            Written = written,
            OpenSearchWriteEnabled = _settings.OpenSearch.WriteEnabled
        };
    }

    private static string? BuildRawPreview(JsonElement? raw)
    {
        if (raw is null)
            return null;

        var text = raw.Value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => raw.Value.GetString(),
            _ => raw.Value.GetRawText()
        };

        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= 512 ? text : text[..512];
    }
}
