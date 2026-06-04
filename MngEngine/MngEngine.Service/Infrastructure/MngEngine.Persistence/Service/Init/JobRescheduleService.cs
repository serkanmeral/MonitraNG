using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using MngEngine.Persistence.Service.HostedService;

namespace MngEngine.Persistence.Service.Init;

/// <summary>
/// EngineConfigPayload ve engineConfigSync'e göre CollectorJob (period grupları), SendJob, ConfigSyncJob'ı yeniden zamanlar.
/// </summary>
public class JobRescheduleService : IJobRescheduleService
{
    private readonly QuartzHostedService _quartzHostedService;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IMemoryCache _cache;
    private readonly SecEventQueueOptions _secEventQueueOptions;

    public JobRescheduleService(
        QuartzHostedService quartzHostedService,
        IEngineConfigProvider configProvider,
        IMemoryCache cache,
        IOptions<SecEventQueueOptions> secEventQueueOptions)
    {
        _quartzHostedService = quartzHostedService;
        _configProvider = configProvider;
        _cache = cache;
        _secEventQueueOptions = secEventQueueOptions.Value;
    }

    public async Task RescheduleJobsAsync(CancellationToken cancellationToken = default)
    {
        var config = _configProvider.GetConfig();
        if (config == null) return;

        // CollectorJob: asset period gruplarına göre birden fazla trigger
        var periodTriggers = BuildCollectorPeriodTriggers();
        await _quartzHostedService.RescheduleCollectorTriggersAsync(periodTriggers, cancellationToken);

        // SendJob: veri gönderim cron
        var sendCron = !string.IsNullOrEmpty(config.SendSchedule) ? config.SendSchedule : "0 */5 * * * ?";
        await _quartzHostedService.RescheduleJobAsync(
            "MngEngine.Persistence.Jobs.SendJob",
            sendCron,
            cancellationToken);

        // ConfigSyncJob: periyodik sync
        var syncMins = config.ConfigSyncPeriodMinutes > 0 ? config.ConfigSyncPeriodMinutes : 10;
        await _quartzHostedService.RescheduleJobAsync(
            "MngEngine.Persistence.Jobs.ConfigSyncJob",
            $"0 */{syncMins} * * * ?",
            cancellationToken);

        var secEventSecs = _secEventQueueOptions.SendIntervalSeconds;
        if (secEventSecs < 5) secEventSecs = 5;
        await _quartzHostedService.RescheduleJobAsync(
            "MngEngine.Persistence.Jobs.SecEventSendJob",
            $"0/{secEventSecs} * * * * ?",
            cancellationToken);
    }

    /// <summary>engineConfigSync AssetConfigs'ı period'a göre gruplar. Her grup için (periodExpr, cronExpr) döner.</summary>
    private List<(string PeriodExpression, string QuartzCron)> BuildCollectorPeriodTriggers()
    {
        var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");
        var configs = syncResult?.AssetConfigs ?? [];

        var periods = configs
            .Select(a => (a.PeriodExpression ?? "").Trim())
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        if (periods.Count == 0)
            return []; // Varsayılan trigger kullanılacak (0/15 * * * * ?)

        return periods
            .Select(p => (PeriodExpression: p, QuartzCron: string.IsNullOrEmpty(p) ? "0/15 * * * * ?" : p))
            .ToList();
    }
}
