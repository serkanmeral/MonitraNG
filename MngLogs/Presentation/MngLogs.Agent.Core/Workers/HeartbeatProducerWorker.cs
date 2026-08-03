using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Metrics;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Workers;

/// <summary>Periodic host.up (+ CPU/memory/disk + optional top-process summaries).</summary>
public sealed class HeartbeatProducerWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly IAgentConfigStore _config;
    private readonly IHostMetricsCollector _metrics;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<HeartbeatProducerWorker> _logger;

    public HeartbeatProducerWorker(
        IOutboundQueue queue,
        IAgentConfigStore config,
        IHostMetricsCollector metrics,
        AgentRuntimeStatus status,
        ILogger<HeartbeatProducerWorker> logger)
    {
        _queue = queue;
        _config = config;
        _metrics = metrics;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProduceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var seconds = Math.Max(5, _config.Current.Policy.HeartbeatIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProduceAsync(stoppingToken);
        }
    }

    private async Task ProduceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var policy = _config.Current.Policy;
            if (!policy.Metrics.Enabled)
                return;

            var items = _metrics.Collect(policy.Metrics.IncludeHostResources).ToList();
            _status.UpdateHostInventory(_metrics.CaptureInventory());

            if (policy.Metrics.IncludeTopProcesses)
            {
                var top = _metrics.CollectTopProcesses(policy.Metrics.TopProcessCount);
                _status.UpdateTopProcesses(top);
                items.AddRange(_metrics.ToTopProcessEvents(top));
            }

            foreach (var item in items)
                await _queue.EnqueueAsync(item, cancellationToken);

            _status.MarkHeartbeat(items.Count);
            _logger.LogDebug("Metrics enqueued count={Count}", items.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Metrics enqueue failed");
        }
    }
}
