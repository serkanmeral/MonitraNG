using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Persistence.Jobs;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Service.HostedService;
using Serilog;

namespace MngEngine.Persistence.Service.Init
{
    public class InitApplicationService : IInitApplicationService
    {
        private readonly QuartzHostedService _quartzHostedService;
        private readonly IConfigService _configService;
        private readonly IEngineConfigProvider _configProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfigSyncClient _configSyncClient;
        private readonly IMqttEngineSubscriber _mqttSubscriber;
        private readonly MqttSyncTriggerService _mqttSyncTrigger;
        private readonly IJobRescheduleService _jobRescheduleService;
        private readonly ILogger _logger;

        public InitApplicationService(
            QuartzHostedService quartzHostedService,
            IConfigService configService,
            IEngineConfigProvider configProvider,
            IMemoryCache memoryCache,
            IConfigSyncClient configSyncClient,
            IMqttEngineSubscriber mqttSubscriber,
            MqttSyncTriggerService mqttSyncTrigger,
            IJobRescheduleService jobRescheduleService,
            ILogger logger)
        {
            _quartzHostedService = quartzHostedService;
            _configService = configService;
            _configProvider = configProvider;
            _memoryCache = memoryCache;
            _configSyncClient = configSyncClient;
            _mqttSubscriber = mqttSubscriber;
            _mqttSyncTrigger = mqttSyncTrigger;
            _jobRescheduleService = jobRescheduleService;
            _logger = logger;
        }

        private async Task StartQuartz(EngineConfigPayload config)
        {
            await _quartzHostedService.StartAsync(CancellationToken.None);
            await _jobRescheduleService.RescheduleJobsAsync(CancellationToken.None);
        }

        /// <summary>Reactor'dan config sync alır, cache'i günceller, Quartz job'larını yapılandırır. Config uygulandıktan hemen sonra çağrılabilir.</summary>
        private async Task SyncAndRescheduleAsync()
        {
            var config = _configProvider.GetConfig();
            var engineId = config?.EngineId;
            var domain = config?.Domain;

            _logger.Information("Config sync başlatılıyor. EngineId={EngineId}, ServerUrl={ServerUrl}", engineId ?? "(yok)", config?.ServerUrl ?? "(yok)");

            if (!string.IsNullOrEmpty(engineId))
            {
                var syncResult = await _configSyncClient.GetConfigAsync(engineId);
                if (syncResult != null)
                {
                    _memoryCache.Set("engineConfigSync", syncResult);
                    var assetsArray = ConfigSyncJob.ToLegacyEngineAssets(syncResult);
                    _memoryCache.Set("engineAssets", assetsArray);
                    _memoryCache.Set("lastSyncAt", DateTime.UtcNow);
                    _logger.Information("Config sync tamamlandı. Agent={Count}, AssetConfig={AssetCount}", syncResult.Agents.Count, syncResult.AssetConfigs.Count);
                }
                else
                {
                    _logger.Warning("Config sync başarısız; engineAssets boş kalacak. Config string ve Reactor erişilebilirliğini kontrol edin.");
                }
            }
            else
            {
                _logger.Information("EngineId yok; config string girişi yapılmamış veya geçersiz.");
            }

            if (config != null)
            {
                await StartQuartz(config);
            }

            if (!string.IsNullOrEmpty(engineId) && !string.IsNullOrEmpty(domain))
            {
                await _mqttSubscriber.StartAsync(domain, engineId);
            }
        }

        public async Task InitApplication()
        {
            await _configService.InitConfig();
            await SyncAndRescheduleAsync();
        }

        /// <summary>MQTT veya manuel tetikleme. ConfigSyncPeriodMinutes dolmadan tekrar sync yapılmaz (gereksiz log ve yük önlenir).</summary>
        public async Task RunConfigSyncAndRescheduleAsync()
        {
            var periodMinutes = 10;
            if (_memoryCache.TryGetValue("engineConfigPayload", out EngineConfigPayload? cachedPayload) && cachedPayload != null && cachedPayload.ConfigSyncPeriodMinutes > 0)
                periodMinutes = cachedPayload.ConfigSyncPeriodMinutes;

            if (_memoryCache.TryGetValue("lastSyncAt", out DateTime lastSyncAt))
            {
                var minInterval = TimeSpan.FromMinutes(periodMinutes);
                if (DateTime.UtcNow - lastSyncAt < minInterval)
                {
                    _logger.Debug("MQTT/config sync tetiklendi ama son sync {Seconds}s önce yapıldı, min aralık {Minutes} dk; atlanıyor.",
                        (int)(DateTime.UtcNow - lastSyncAt).TotalSeconds, periodMinutes);
                    return;
                }
            }

            await SyncAndRescheduleAsync();
        }
    }
}
