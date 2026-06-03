using System.Text.Json.Nodes;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Configuration;
using Microsoft.Extensions.Options;

namespace MngReactor.Persistence.Services.Engine;

public class EngineIdsForAssetResolver : IEngineIdsForAssetResolver
{
    private readonly IDataGatewayClient _dg;
    private readonly IOptions<MngReactorSettings> _options;

    public EngineIdsForAssetResolver(IDataGatewayClient dg, IOptions<MngReactorSettings> options)
    {
        _dg = dg;
        _options = options;
    }

    public async Task<IReadOnlyList<string>> GetEngineIdsForAssetAsync(string domain, string assetId, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        var token = ResolveToken(domain, accessToken);
        if (string.IsNullOrEmpty(token))
            return [];

        // ElemMatch: asset_configs içinde assetId eşleşen agent'ları bul, engineId'leri al
        var pipeline = new JsonArray
        {
            new JsonObject
            {
                ["$match"] = new JsonObject
                {
                    ["asset_configs"] = new JsonObject
                    {
                        ["$elemMatch"] = new JsonObject { ["assetId"] = assetId }
                    }
                }
            },
            new JsonObject { ["$project"] = new JsonObject { ["engineId"] = 1 } }
        };

        var results = await _dg.AggregateAsync("mon_agents", pipeline, token, cancellationToken);
        var engineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in results)
        {
            if (item is JsonObject jo)
            {
                var id = jo["engineId"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(id))
                    engineIds.Add(id);
            }
        }
        return engineIds.ToList();
    }

    private string? ResolveToken(string domain, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) return accessToken;
        return _options.Value?.DataGateway?.DomainTokens?.GetValueOrDefault(domain);
    }
}
