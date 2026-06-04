using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QueueController : ControllerBase
{
    private readonly IMetricBatchQueue _queue;
    private readonly IMemoryCache _cache;

    public QueueController(IMetricBatchQueue queue, IMemoryCache cache)
    {
        _queue = queue;
        _cache = cache;
    }

    /// <summary>
    /// Queue'daki batch'lerin özet listesini döner (tüketmeden).
    /// agentName, assetName, itemName config sync'ten resolve edilir.
    /// </summary>
    [HttpGet]
    public IActionResult GetQueue()
    {
        var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");
        var configs = syncResult?.AssetConfigs ?? [];

        var batches = _queue.PeekAll();
        var items = batches.Select(b =>
        {
            var ac = configs.FirstOrDefault(c => c.AgentId == b.AgentId && c.AssetId == b.AssetId);
            var agentName = ac?.AgentName ?? "";
            var assetName = ac?.AssetName ?? "";
            var itemName = !string.IsNullOrEmpty(b.ItemId)
                ? configs.FirstOrDefault(c => c.AgentId == b.AgentId && c.AssetId == b.AssetId && c.ItemId == b.ItemId)?.ItemName ?? ac?.ItemName ?? ""
                : ac?.ItemName ?? "";
            return new
            {
                b.AssetId,
                b.AgentId,
                b.ItemId,
                agentName,
                assetName,
                itemName,
                b.CollectedAt,
                MetricCount = b.Metrics?.Count ?? 0,
                Metrics = b.Metrics?.Select(m => new { m.CollectibleCode, m.Value, m.Unit }) ?? []
            };
        }).ToList();
        return Ok(new { count = _queue.Count, items });
    }
}
