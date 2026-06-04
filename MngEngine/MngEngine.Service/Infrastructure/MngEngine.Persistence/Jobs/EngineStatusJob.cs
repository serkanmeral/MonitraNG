using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Internal;

using Quartz;
using Serilog;

namespace MngEngine.Persistence.Jobs;

public class EngineStatusJob : IJob
{
    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IEngineErrorBuffer _errorBuffer;
    private readonly IEngineStatusClient _statusClient;
    private readonly IMetricBatchQueue _queue;
    private readonly IMemoryCache _cache;

    public EngineStatusJob(
        ILogger logger,
        IEngineConfigProvider configProvider,
        IEngineErrorBuffer errorBuffer,
        IEngineStatusClient statusClient,
        IMetricBatchQueue queue,
        IMemoryCache cache)
    {
        _logger = logger;
        _configProvider = configProvider;
        _errorBuffer = errorBuffer;
        _statusClient = statusClient;
        _queue = queue;
        _cache = cache;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var config = _configProvider.GetConfig();
        if (config == null || string.IsNullOrEmpty(config.EngineId) || string.IsNullOrEmpty(config.Domain))
        {
            _logger.Information("EngineStatusJob: Atlanıyor (Config/EngineId/Domain yok – config string girilmemiş olabilir)");
            return;
        }

        var errors = _errorBuffer.GetRecent(50);
        var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");
        var assetCount = syncResult?.AssetConfigs?.Count ?? 0;

        var health = errors.Count == 0 ? "ok" : "degraded";
        var payload = new EngineStatusPayload(
            config.EngineId,
            config.Domain,
            DateTime.UtcNow,
            health,
            errors.Select(e => new EngineStatusErrorItem(e.AssetId, e.AgentId, e.ErrorCode, e.Message, e.OccurredAt)).ToList(),
            _queue.Count,
            assetCount,
            HostAddressHelper.GetLocalAddress());

        var ok = await _statusClient.SendStatusAsync(payload, context.CancellationToken);
        if (ok)
            _logger.Information("EngineStatusJob: Status gönderildi engineId={EngineId} health={Health} hostAddress={HostAddress} errorCount={ErrorCount}",
                config.EngineId, health, payload.HostAddress ?? "(yok)", errors.Count);
        else
            _logger.Warning("EngineStatusJob: Status gönderilemedi engineId={EngineId}", config.EngineId);
    }
}
