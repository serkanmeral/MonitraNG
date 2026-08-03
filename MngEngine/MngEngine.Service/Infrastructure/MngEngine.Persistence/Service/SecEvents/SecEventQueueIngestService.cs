using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed class SecEventQueueIngestService : ISecEventQueueIngestService
{
    private readonly ILogger _logger;
    private readonly ISecEventBatchQueue _queue;
    private readonly ISecEventSendProcessing _sendProcessing;
    private readonly SecEventSendCoordinator _sendCoordinator;
    private readonly SecEventQueueOptions _options;

    public SecEventQueueIngestService(
        ILogger logger,
        ISecEventBatchQueue queue,
        ISecEventSendProcessing sendProcessing,
        SecEventSendCoordinator sendCoordinator,
        IOptions<SecEventQueueOptions> options)
    {
        _logger = logger;
        _queue = queue;
        _sendProcessing = sendProcessing;
        _sendCoordinator = sendCoordinator;
        _options = options.Value;
    }

    public async Task<SecEventWecBatchResponse> IngestWecBatchAsync(
        SecEventWecBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return new SecEventWecBatchResponse
            {
                Enqueued = 0,
                QueueDepth = _queue.Count,
                Flushed = false
            };
        }

        var enqueued = 0;
        var rejectedNxlog = 0;
        foreach (var item in request.Items)
        {
            var source = item.Source ?? new SecEventIngestSource
            {
                Type = "ad",
                Product = "windows",
                Host = _options.DefaultWecHost ?? "wec"
            };

            var enqueueItem = new SecEventIngestItem
            {
                ReceivedAt = item.ReceivedAt == default ? DateTime.UtcNow : item.ReceivedAt,
                Source = source,
                Raw = ToRawObject(item.Raw)
            };

            if (SecEventNxlogIngestGuard.ShouldReject(enqueueItem, _options.AcceptNxlogIngest))
            {
                rejectedNxlog++;
                continue;
            }

            _queue.Enqueue(enqueueItem);
            enqueued++;
        }

        if (rejectedNxlog > 0)
        {
            _logger.Warning(
                "WEC batch rejected {Rejected} NXLog item(s); AcceptNxlogIngest=false. Enqueued={Enqueued}",
                rejectedNxlog,
                enqueued);
        }

        _logger.Information(
            "WEC batch enqueued {Count} item(s); queueDepth={Depth}",
            enqueued,
            _queue.Count);

        _sendCoordinator.RequestFlushIfThresholdReached();

        if (!request.AutoFlush)
        {
            return new SecEventWecBatchResponse
            {
                Enqueued = enqueued,
                QueueDepth = _queue.Count,
                Flushed = false
            };
        }

        var result = await _sendProcessing.FlushAsync(cancellationToken);
        return new SecEventWecBatchResponse
        {
            Enqueued = enqueued,
            QueueDepth = _queue.Count,
            Flushed = true,
            Accepted = result.Accepted,
            Published = result.Published
        };
    }

    private static object ToRawObject(JsonElement raw)
    {
        if (raw.ValueKind == JsonValueKind.String)
            return raw.GetString() ?? string.Empty;

        return JsonNode.Parse(raw.GetRawText()) ?? raw.GetRawText();
    }
}
