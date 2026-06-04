using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Jobs;

namespace MngEngine.Api.Controllers;

[Route("api/config")]
[ApiController]
public class ConfigStatusController : ControllerBase
{
    private static readonly string ConfigSyncJobFullName = typeof(ConfigSyncJob).FullName!;

    private readonly IEngineConfigProvider _configProvider;
    private readonly IMemoryCache _cache;
    private readonly IJobService _jobService;

    public ConfigStatusController(IEngineConfigProvider configProvider, IMemoryCache cache, IJobService jobService)
    {
        _configProvider = configProvider;
        _cache = cache;
        _jobService = jobService;
    }

    /// <summary>
    /// Config yüklü mü, engineId, son sync zamanı, agent/asset sayıları.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetConfigStatus()
    {
        var config = _configProvider.GetConfig();
        var hasConfig = config != null;
        var lastSyncAt = _cache.Get<DateTime?>("lastSyncAt");
        var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");

        int? agentCount = syncResult?.Agents?.Count;
        int? assetConfigCount = syncResult?.AssetConfigs?.Count;

        return Ok(new
        {
            hasConfig,
            engineId = config?.EngineId ?? (object?)null,
            engineName = config?.EngineName ?? (object?)null,
            domain = config?.Domain ?? (object?)null,
            serverUrl = config?.ServerUrl ?? (object?)null,
            mqttUrl = config?.MqttUrl ?? (object?)null,
            lastSyncAt = lastSyncAt?.ToString("O"),
            agentCount,
            assetConfigCount
        });
    }

    /// <summary>
    /// ConfigSyncJob'u manuel tetikler. Son sync zamanı ve agent/asset sayıları güncellenir.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> TriggerConfigSync()
    {
        var triggered = await _jobService.TriggerJobAsync(ConfigSyncJobFullName);
        if (!triggered)
            return NotFound(new { success = false, message = "ConfigSyncJob bulunamadı." });
        return Ok(new { success = true, message = "Config sync tetiklendi." });
    }
}
