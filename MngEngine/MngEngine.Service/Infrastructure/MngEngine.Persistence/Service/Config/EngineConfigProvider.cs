using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;

namespace MngEngine.Persistence.Service.Config;

public class EngineConfigProvider : IEngineConfigProvider
{
    private const string EngineConfigPayloadCacheKey = "engineConfigPayload";

    private readonly IMemoryCache _memoryCache;

    public EngineConfigProvider(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public EngineConfigPayload? GetConfig() =>
        _memoryCache.TryGetValue(EngineConfigPayloadCacheKey, out EngineConfigPayload? payload)
            ? payload
            : null;
}
