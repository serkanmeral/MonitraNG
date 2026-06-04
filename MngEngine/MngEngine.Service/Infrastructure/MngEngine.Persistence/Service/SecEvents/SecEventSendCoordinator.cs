using Microsoft.Extensions.Options;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

/// <summary>Eşik aşıldığında fire-and-forget flush tetikler.</summary>
public sealed class SecEventSendCoordinator
{
    private readonly ILogger _logger;
    private readonly ISecEventBatchQueue _queue;
    private readonly ISecEventSendProcessing _sendProcessing;
    private readonly int _batchThreshold;
    private int _flushRunning;

    public SecEventSendCoordinator(
        ILogger logger,
        ISecEventBatchQueue queue,
        ISecEventSendProcessing sendProcessing,
        IOptions<SecEventQueueOptions> options)
    {
        _logger = logger;
        _queue = queue;
        _sendProcessing = sendProcessing;
        _batchThreshold = Math.Max(1, options.Value.BatchThreshold);
    }

    public void RequestFlushIfThresholdReached()
    {
        if (_queue.Count < _batchThreshold)
            return;

        if (Interlocked.CompareExchange(ref _flushRunning, 1, 0) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _sendProcessing.FlushAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SecEvent threshold flush hatası");
            }
            finally
            {
                Interlocked.Exchange(ref _flushRunning, 0);
            }
        });
    }
}
