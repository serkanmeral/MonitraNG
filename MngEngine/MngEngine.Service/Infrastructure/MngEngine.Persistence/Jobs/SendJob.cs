using MngEngine.Application.Features.Ingest;
using MngEngine.Application.Interfaces;
using Quartz;
using Serilog;

namespace MngEngine.Persistence.Jobs;

public class SendJob : IJob
{
    private readonly ILogger _logger;
    private readonly IMetricBatchQueue _queue;
    private readonly IIngestClient _ingestClient;

    public SendJob(ILogger logger, IMetricBatchQueue queue, IIngestClient ingestClient)
    {
        _logger = logger;
        _queue = queue;
        _ingestClient = ingestClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var batches = _queue.DequeueAll();
        if (batches.Count == 0)
        {
            _logger.Debug("SendJob: Kuyrukta batch yok");
            return;
        }

        _logger.Information("SendJob: {Count} batch gönderiliyor", batches.Count);

        var request = new IngestMetricsRequest { Batches = batches.ToList() };
        var result = await _ingestClient.SendAsync(request, context.CancellationToken);

        if (result.Success)
            _logger.Information("SendJob tamamlandı. Saved={Saved}, Failed={Failed}", result.SavedCount, result.FailedCount);
        else
            _logger.Warning("SendJob başarısız: {Error}", result.ErrorMessage);
    }
}
