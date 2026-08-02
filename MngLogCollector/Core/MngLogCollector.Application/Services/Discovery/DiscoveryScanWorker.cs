using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngLogCollector.Application.Abstractions.Discovery;

namespace MngLogCollector.Application.Services.Discovery;

public sealed class DiscoveryScanWorker : BackgroundService
{
    private readonly IDiscoveryScanQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DiscoveryScanWorker> _logger;

    public DiscoveryScanWorker(
        IDiscoveryScanQueue queue,
        IServiceScopeFactory scopes,
        ILogger<DiscoveryScanWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (databaseName, runId) in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<DiscoveryScanRunner>();
                await runner.RunAsync(databaseName, runId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Discovery scan worker failed run={RunId}", runId);
                try
                {
                    using var scope = _scopes.CreateScope();
                    var jobs = scope.ServiceProvider.GetRequiredService<IDiscoveryScanJobStore>();
                    var job = await jobs.GetAsync(databaseName, runId, CancellationToken.None);
                    if (job is not null && job.Status is "queued" or "running")
                    {
                        job.Status = "failed";
                        job.Error = ex.Message;
                        job.CompletedAt = DateTime.UtcNow;
                        await jobs.UpdateAsync(databaseName, job, CancellationToken.None);
                    }
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to mark scan job failed run={RunId}", runId);
                }
            }
        }
    }
}
