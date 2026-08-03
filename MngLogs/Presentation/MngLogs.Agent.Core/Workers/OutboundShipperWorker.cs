using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.Transport;

namespace MngLogs.Agent.Workers;

public sealed class OutboundShipperWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly ICollectorClient _client;
    private readonly IAgentConfigStore _config;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<OutboundShipperWorker> _logger;

    public OutboundShipperWorker(
        IOutboundQueue queue,
        ICollectorClient client,
        IAgentConfigStore config,
        AgentRuntimeStatus status,
        ILogger<OutboundShipperWorker> logger)
    {
        _queue = queue;
        _client = client;
        _config = config;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var seconds = Math.Max(2, _config.Current.Policy.ShipIntervalSeconds);
            try
            {
                await ShipOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ship loop error");
                _status.MarkShipAttempt(0, false, ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ShipOnceAsync(CancellationToken cancellationToken)
    {
        var settings = _config.Current;
        var max = Math.Max(1, settings.Policy.MaxEventsPerBatch);
        var batch = await _queue.DequeueBatchAsync(max, cancellationToken);
        if (batch.Count == 0)
            return;

        var healthy = await _client.HealthAsync(cancellationToken);
        _status.MarkCollectorHealth(healthy);
        if (!healthy)
        {
            _status.MarkShipAttempt(batch.Count, false, "collector unhealthy / unreachable");
            return;
        }

        var request = new IngestBatchRequest
        {
            Domain = settings.Policy.Domain,
            HostId = _config.ResolveHostId(),
            Hostname = Environment.MachineName,
            Events = batch.Select(b => b.Item).ToList()
        };

        var response = await _client.SendBatchAsync(request, cancellationToken);
        if (response == null)
        {
            _status.MarkShipAttempt(batch.Count, false, "ingest HTTP failed");
            return;
        }

        _queue.Complete(batch.Select(b => b.FilePath));
        _status.MarkShipAttempt(batch.Count, true);
        _status.RecordShipped(batch.Select(b => b.Item));
        _logger.LogInformation(
            "Shipped batch accepted={Accepted} written={Written} pending={Pending}",
            response.Accepted,
            response.Written,
            _queue.PendingCount);
    }
}
