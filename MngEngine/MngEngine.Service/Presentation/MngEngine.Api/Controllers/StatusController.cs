using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IMetricBatchQueue _queue;
    private readonly IMemoryCache _cache;

    public StatusController(IJobService jobService, IMetricBatchQueue queue, IMemoryCache cache)
    {
        _jobService = jobService;
        _queue = queue;
        _cache = cache;
    }

    /// <summary>
    /// Toplama durumu: agent/asset sayısı, job sayısı, queue batch sayısı.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var jobs = await _jobService.GetJobs();
        var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");

        var agentCount = syncResult?.Agents?.Count ?? 0;
        var assetCount = syncResult?.AssetConfigs?.Count ?? 0;

        return Ok(new
        {
            agentCount,
            assetCount,
            jobCount = jobs?.Count() ?? 0,
            queueBatchCount = _queue.Count
        });
    }
}
