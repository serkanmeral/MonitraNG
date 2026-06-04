using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed class SecEventSendProcessing : ISecEventSendProcessing
{
    private readonly ILogger _logger;
    private readonly ISecEventBatchQueue _queue;
    private readonly ISecEventIngestClient _ingestClient;

    public SecEventSendProcessing(
        ILogger logger,
        ISecEventBatchQueue queue,
        ISecEventIngestClient ingestClient)
    {
        _logger = logger;
        _queue = queue;
        _ingestClient = ingestClient;
    }

    public async Task<SecEventIngestResult> FlushAsync(CancellationToken ct = default)
    {
        var items = _queue.DequeueAll();
        if (items.Count == 0)
            return new SecEventIngestResult { Success = true };

        _logger.Information("SecEvent flush: {Count} item Reactor'a gönderiliyor", items.Count);

        var result = await _ingestClient.SendAsync(new SecEventIngestRequest { Items = items.ToList() }, ct);
        if (result.Success)
        {
            _logger.Information("SecEvent flush tamamlandı. Accepted={Accepted}, Published={Published}",
                result.Accepted, result.Published);
        }
        else
        {
            _logger.Warning("SecEvent flush başarısız: {Error}", result.ErrorMessage);
        }

        return result;
    }
}
