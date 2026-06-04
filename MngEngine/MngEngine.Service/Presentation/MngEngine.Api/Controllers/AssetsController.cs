using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AssetsController : ControllerBase
{
    private readonly IMemoryCache _cache;
    private readonly IMetricBatchQueue _queue;

    public AssetsController(IMemoryCache cache, IMetricBatchQueue queue)
    {
        _cache = cache;
        _queue = queue;
    }

    /// <summary>
    /// Config sync'ten gelen asset listesini döner. UI ile uyumlu basit DTO.
    /// agentName, assetName, itemName ve son başarılı okuma zamanı dahil.
    /// </summary>
    [HttpGet]
    public IActionResult GetAssets()
    {
        var syncResult = _cache.Get<EngineConfigSyncResult?>("engineConfigSync");
        var configs = syncResult?.AssetConfigs ?? [];
        var lastCollected = _queue.GetLastCollectedByAsset();
        var assets = configs.Select((a, i) =>
        {
            var lastAt = lastCollected.TryGetValue(a.AssetId, out var dt) ? dt : (DateTime?)null;
            return new
            {
                id = $"{a.AgentId}-{a.AssetId}-{i}",
                agentId = a.AgentId,
                assetId = a.AssetId,
                itemId = a.ItemId ?? "",
                agentName = a.AgentName ?? "",
                assetName = a.AssetName ?? "",
                itemName = a.ItemName ?? "",
                collectionMethod = a.CollectionMethod,
                collectibles = a.Collectibles,
                connectionInfo = a.ConnectionInfo,
                lastCollectedAt = lastAt != null ? lastAt.Value.ToString("o") : (string?)null
            };
        }).ToList();
        return Ok(assets);
    }
}
