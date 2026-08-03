using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;

namespace MngLogs.Agent.Workers;

/// <summary>Periodically refreshes the local server package catalog cache.</summary>
public sealed class PackageCatalogSyncWorker(
    IEventLogPackageCatalogStore catalog,
    IAgentConfigStore config,
    ILogger<PackageCatalogSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial sync shortly after start.
        try
        {
            // Force on boot so a catalog filtered by an older agent is not kept via 304.
            await catalog.RefreshAsync(force: true, stoppingToken);
            logger.LogInformation(
                "Event log package catalog synced from {Source} at {At:o}",
                catalog.Source,
                catalog.LastSyncedUtc);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Initial package catalog sync failed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Clamp(
                config.Current.Policy.EventLog.PackageCatalogSyncIntervalSeconds,
                0,
                86_400);

            if (interval <= 0)
                interval = 3600;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await catalog.RefreshAsync(force: false, stoppingToken);
                logger.LogDebug(
                    "Event log package catalog refreshed ({Source}, {Count} packages)",
                    catalog.Source,
                    catalog.ServerPackages.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Package catalog sync failed; keeping last known catalog");
            }
        }
    }
}
