using Microsoft.Extensions.Options;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed class SecEventSendProcessing : ISecEventSendProcessing
{
    private readonly ILogger _logger;
    private readonly ISecEventBatchQueue _queue;
    private readonly ISecEventIngestClient _ingestClient;
    private readonly int _maxReactorBatchItems;
    private readonly int _retryCount;
    private readonly int _retryDelayMs;

    public SecEventSendProcessing(
        ILogger logger,
        ISecEventBatchQueue queue,
        ISecEventIngestClient ingestClient,
        IOptions<SecEventQueueOptions> options)
    {
        _logger = logger;
        _queue = queue;
        _ingestClient = ingestClient;
        var o = options.Value;
        _maxReactorBatchItems = o.MaxReactorBatchItems > 0 ? o.MaxReactorBatchItems : 200;
        _retryCount = Math.Max(0, o.ReactorSendRetryCount);
        _retryDelayMs = Math.Max(0, o.ReactorSendRetryDelayMs);
    }

    public async Task<SecEventIngestResult> FlushAsync(CancellationToken ct = default)
    {
        var items = _queue.DequeueAll();
        if (items.Count == 0)
            return new SecEventIngestResult { Success = true };

        _logger.Information("SecEvent flush: {Count} item Reactor'a gönderiliyor", items.Count);

        var totalAccepted = 0;
        var totalRejected = 0;
        var totalPublished = 0;
        string? lastError = null;
        var anyFailure = false;

        foreach (var chunk in Chunk(items, _maxReactorBatchItems))
        {
            var result = await SendChunkWithRetryAsync(chunk, ct);
            if (result.Success)
            {
                totalAccepted += result.Accepted;
                totalRejected += result.Rejected;
                totalPublished += result.Published;
            }
            else
            {
                anyFailure = true;
                lastError = result.ErrorMessage;
                foreach (var item in chunk)
                    _queue.Enqueue(item);

                _logger.Warning(
                    "SecEvent flush chunk başarısız; {Count} item kuyruğa geri alındı: {Error}",
                    chunk.Count,
                    lastError);
            }
        }

        if (!anyFailure)
        {
            _logger.Information(
                "SecEvent flush tamamlandı. Accepted={Accepted}, Published={Published}",
                totalAccepted,
                totalPublished);
        }

        return new SecEventIngestResult
        {
            Success = !anyFailure,
            Accepted = totalAccepted,
            Rejected = totalRejected,
            Published = totalPublished,
            ErrorMessage = lastError
        };
    }

    private async Task<SecEventIngestResult> SendChunkWithRetryAsync(
        IReadOnlyList<SecEventIngestItem> chunk,
        CancellationToken ct)
    {
        SecEventIngestResult? last = null;
        var attempts = _retryCount + 1;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            last = await _ingestClient.SendAsync(
                new SecEventIngestRequest { Items = chunk.ToList() },
                ct);

            if (last.Success)
                return last;

            if (attempt >= attempts)
                break;

            _logger.Warning(
                "SecEvent Reactor gönderimi deneme {Attempt}/{Attempts} başarısız; {DelayMs}ms sonra tekrar: {Error}",
                attempt,
                attempts,
                _retryDelayMs,
                last.ErrorMessage);

            if (_retryDelayMs > 0)
                await Task.Delay(_retryDelayMs, ct);
        }

        return last ?? new SecEventIngestResult
        {
            Success = false,
            Rejected = chunk.Count,
            ErrorMessage = "Reactor gönderimi başarısız"
        };
    }

    private static IEnumerable<IReadOnlyList<SecEventIngestItem>> Chunk(
        IReadOnlyList<SecEventIngestItem> items,
        int size)
    {
        for (var i = 0; i < items.Count; i += size)
        {
            var take = Math.Min(size, items.Count - i);
            var slice = new List<SecEventIngestItem>(take);
            for (var j = 0; j < take; j++)
                slice.Add(items[i + j]);

            yield return slice;
        }
    }
}
