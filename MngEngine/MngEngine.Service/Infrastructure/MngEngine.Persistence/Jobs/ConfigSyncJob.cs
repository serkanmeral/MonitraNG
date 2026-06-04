using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Service.Config;
using Quartz;
using Serilog;
using System.Text.Json.Nodes;

namespace MngEngine.Persistence.Jobs;

public class ConfigSyncJob : IJob
{
    private readonly ILogger _logger;
    private readonly IConfigSyncClient _configSyncClient;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IMemoryCache _memoryCache;
    private readonly IJobRescheduleService _jobRescheduleService;

    public ConfigSyncJob(ILogger logger, IConfigSyncClient configSyncClient, IEngineConfigProvider configProvider, IMemoryCache memoryCache, IJobRescheduleService jobRescheduleService)
    {
        _logger = logger;
        _configSyncClient = configSyncClient;
        _configProvider = configProvider;
        _memoryCache = memoryCache;
        _jobRescheduleService = jobRescheduleService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var engineId = GetEngineId();
        if (string.IsNullOrEmpty(engineId))
        {
            _logger.Debug("ConfigSyncJob: engineId bulunamadı");
            return;
        }

        var result = await _configSyncClient.GetConfigAsync(engineId, context.CancellationToken);
        if (result == null)
        {
            _logger.Warning("ConfigSyncJob: Config alınamadı engineId={EngineId}", engineId);
            return;
        }

        var newSignature = ConfigSyncSignature.Compute(result);
        var oldSignature = ConfigSyncSignature.GetStored(_memoryCache);
        if (!string.IsNullOrEmpty(oldSignature) && oldSignature == newSignature)
        {
            _logger.Debug("ConfigSyncJob: Asset/periyot değişikliği yok, sync ve reschedule atlanıyor");
            return;
        }

        _memoryCache.Set("engineConfigSync", result);
        var assetsArray = ToLegacyEngineAssets(result);
        _memoryCache.Set("engineAssets", assetsArray);
        _memoryCache.Set("lastSyncAt", DateTime.UtcNow);
        ConfigSyncSignature.Store(_memoryCache, newSignature);

        // SendSchedule ve ConfigSyncPeriodMinutes UI'dan (mon_engines) sync ile geldi; payload'u güncelle ki RescheduleJobsAsync güncel cron kullansın
        if (_memoryCache.TryGetValue("engineConfigPayload", out EngineConfigPayload? currentPayload) && currentPayload != null)
        {
            var sendSchedule = !string.IsNullOrWhiteSpace(result.SendSchedule) ? result.SendSchedule!.Trim() : currentPayload.SendSchedule;
            var configSyncMins = result.ConfigSyncPeriodMinutes > 0 ? result.ConfigSyncPeriodMinutes : currentPayload.ConfigSyncPeriodMinutes;
            var updated = currentPayload with { SendSchedule = sendSchedule ?? "0 */5 * * * ?", ConfigSyncPeriodMinutes = configSyncMins };
            _memoryCache.Set("engineConfigPayload", updated);
        }

        await _jobRescheduleService.RescheduleJobsAsync(context.CancellationToken);

        _logger.Information("ConfigSyncJob tamamlandı. Agent={AgentCount}, AssetConfig={AssetCount}",
            result.Agents.Count, result.AssetConfigs.Count);
    }

        internal static JsonArray ToLegacyEngineAssets(Application.Features.EngineConfig.EngineConfigSyncResult result)
    {
        var arr = new JsonArray();
        foreach (var ac in result.AssetConfigs)
        {
            var obj = new JsonObject
            {
                ["Asset_Id"] = ac.AssetId,
                ["AgentId"] = ac.AgentId,
                ["ItemId"] = ac.ItemId ?? (JsonNode?)string.Empty,
                ["PeriodExpression"] = ac.PeriodExpression ?? (JsonNode?)string.Empty,
                ["ConnectionInfo"] = CloneOrEmpty(ac.ConnectionInfo),
                ["CollectionMethod"] = ac.CollectionMethod,
                ["Collectibles"] = new JsonArray(ac.Collectibles.Select(c => (JsonNode)new JsonObject
                {
                    ["Code"] = c.Code,
                    ["Enabled"] = c.Enabled
                }).ToArray())
            };
            arr.Add(obj);
        }
        return arr;
    }

    /// <summary>JsonNode parent'a sahip olabilir; yeni ağaca eklemek için kopya oluştur.</summary>
    private static JsonObject CloneOrEmpty(JsonObject? node)
    {
        if (node == null) return new JsonObject();
        var clone = JsonNode.Parse(node.ToJsonString());
        return clone is JsonObject jo ? jo : new JsonObject();
    }

    private string? GetEngineId() => _configProvider.GetConfig()?.EngineId;
}
