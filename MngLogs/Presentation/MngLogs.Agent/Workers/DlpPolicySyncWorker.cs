using MngLogs.Agent.Configuration;
using MngLogs.Agent.Dlp;

namespace MngLogs.Agent.Workers;

public sealed class DlpPolicySyncWorker(
    IDlpPolicyStore store,
    IAgentConfigStore config,
    DlpLocalKeyStore keys,
    ILogger<DlpPolicySyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            keys.GetOrCreate();
            await store.RefreshAsync(force: true, stoppingToken);
            logger.LogInformation(
                "DLP policy synced from {Source} version {Version}",
                store.Source,
                store.Current.Version);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Initial DLP policy sync failed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = Math.Clamp(config.Current.Policy.Dlp.PolicySyncIntervalSeconds, 0, 86_400);
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
                await store.RefreshAsync(force: false, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "DLP policy sync failed; keeping last known policy");
            }
        }
    }
}
