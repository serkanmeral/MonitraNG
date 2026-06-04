using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Jobs;
using MngEngine.Persistence.Service.Config;
using Serilog;

namespace MngEngine.Persistence.Service.HostedService;

/// <summary>
/// MQTT sync mesajı geldiğinde config sync tetikler.
/// </summary>
public class MqttSyncTriggerService
{
    private readonly IMqttEngineSubscriber _mqttSubscriber;
    private readonly IConfigSyncClient _configSyncClient;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IMemoryCache _cache;
    private readonly IJobRescheduleService _jobRescheduleService;
    private readonly ILogger _logger;

    public MqttSyncTriggerService(
        IMqttEngineSubscriber mqttSubscriber,
        IConfigSyncClient configSyncClient,
        IEngineConfigProvider configProvider,
        IMemoryCache cache,
        IJobRescheduleService jobRescheduleService,
        ILogger logger)
    {
        _mqttSubscriber = mqttSubscriber;
        _configSyncClient = configSyncClient;
        _configProvider = configProvider;
        _cache = cache;
        _jobRescheduleService = jobRescheduleService;
        _logger = logger;

        _mqttSubscriber.SyncRequested += OnSyncRequested;
    }

    private async void OnSyncRequested(object? sender, EventArgs e)
    {
        var engineId = GetEngineId();
        if (string.IsNullOrEmpty(engineId))
        {
            _logger.Warning("MQTT sync tetiklendi ancak engineId bulunamadı");
            return;
        }

        _logger.Information("MQTT sync mesajı alındı, config sync başlatılıyor...");

        try
        {
            var result = await _configSyncClient.GetConfigAsync(engineId);
            if (result != null)
            {
                var newSignature = ConfigSyncSignature.Compute(result);
                var oldSignature = ConfigSyncSignature.GetStored(_cache);
                if (!string.IsNullOrEmpty(oldSignature) && oldSignature == newSignature)
                {
                    _logger.Debug("MQTT sync: Asset/periyot değişikliği yok, sync ve reschedule atlanıyor");
                    return;
                }

                _cache.Set("engineConfigSync", result);
                var assetsArray = ConfigSyncJob.ToLegacyEngineAssets(result);
                _cache.Set("engineAssets", assetsArray);
                _cache.Set("lastSyncAt", DateTime.UtcNow);
                ConfigSyncSignature.Store(_cache, newSignature);
                await _jobRescheduleService.RescheduleJobsAsync(CancellationToken.None);
                _logger.Information("MQTT tetikli config sync tamamlandı. Agent={Count}, job'lar yeniden zamanlandı", result.Agents.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MQTT tetikli config sync hatası");
        }
    }

private string? GetEngineId() => _configProvider.GetConfig()?.EngineId;
}
