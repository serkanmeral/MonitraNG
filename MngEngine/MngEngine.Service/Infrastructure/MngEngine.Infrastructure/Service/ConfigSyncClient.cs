using System.Text.Json;
using System.Text.Json.Nodes;
using MngEngine.Application.Features.EngineConfig;
using MngEngine.Application.Interfaces;
using MngEngine.Infrastructure.Context;
using RestSharp;
using Serilog;

namespace MngEngine.Infrastructure.Service;

public class ConfigSyncClient : IConfigSyncClient
{
    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IRestContext _context;

    public ConfigSyncClient(
        ILogger logger,
        IEngineConfigProvider configProvider,
        IAccessTokenProvider tokenProvider,
        IRestContext context)
    {
        _logger = logger;
        _configProvider = configProvider;
        _tokenProvider = tokenProvider;
        _context = context;
    }

    public async Task<EngineConfigSyncResult?> GetConfigAsync(string engineId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(engineId)) return null;

        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        if (string.IsNullOrEmpty(token))
        {
            _logger.Warning("ConfigSync: Token alınamadı");
            return null;
        }

        var baseUrl = GetReactorBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.Warning("ConfigSync: ServerUrl yok (config yüklü mü? EngineConfigPayload kontrol edin)");
            return null;
        }

        var request = new RestRequest("/api/v1/engine/config", Method.Get)
            .AddParameter("engineId", engineId)
            .AddHeader("Authorization", "Bearer " + token);

        try
        {
            var client = _context.RestClient(baseUrl);
            var response = await client.ExecuteAsync(request, ct);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                _logger.Warning("ConfigSync: Reactor yanıtı başarısız. Status={StatusCode}, Content={Content}", response.StatusCode, response.Content ?? "(boş)");
                return null;
            }

            var doc = JsonNode.Parse(response.Content)?.AsObject();
            if (doc == null) return null;

            var result = MapToResult(doc);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Config sync başarısız");
            return null;
        }
    }

    /// <summary>JsonObject indexer case-sensitive; Reactor camelCase veya PascalCase dönebilir.</summary>
    private static string GetStr(JsonObject obj, string camelKey, string pascalKey)
    {
        var node = obj[camelKey] ?? obj[pascalKey];
        return node?.GetValue<string>()?.Trim() ?? "";
    }

    private static string? GetStrOrNull(JsonObject obj, string camelKey, string pascalKey)
    {
        var node = obj[camelKey] ?? obj[pascalKey];
        var s = node?.GetValue<string>()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static EngineConfigSyncResult MapToResult(JsonObject doc)
    {
        var agents = new List<EngineConfigAgent>();
        var agentArr = (doc["agents"] ?? doc["Agents"]) as JsonArray;
        if (agentArr != null)
        {
            foreach (var a in agentArr)
            {
                if (a is not JsonObject ao) continue;
                agents.Add(new EngineConfigAgent
                {
                    AgentId = GetStr(ao, "agentId", "AgentId"),
                    Name = GetStr(ao, "name", "Name"),
                    Status = GetStr(ao, "status", "Status")
                });
            }
        }

        var assetConfigs = new List<EngineConfigAsset>();
        var acArr = (doc["assetConfigs"] ?? doc["AssetConfigs"]) as JsonArray;
        if (acArr != null)
        {
            foreach (var ac in acArr)
            {
                if (ac is not JsonObject aco) continue;
                var collectibles = new List<EngineConfigCollectible>();
                var collArr = (aco["collectibles"] ?? aco["Collectibles"]) as JsonArray;
                if (collArr != null)
                {
                    foreach (var c in collArr)
                    {
                        if (c is not JsonObject co) continue;
                        if ((co["enabled"] ?? co["Enabled"])?.GetValue<bool>() != true) continue;
                        collectibles.Add(new EngineConfigCollectible
                        {
                            Code = GetStr(co, "code", "Code"),
                            Enabled = true
                        });
                    }
                }
                var periodObj = (aco["period"] ?? aco["Period"]) as JsonObject;
                var periodExpression = periodObj != null ? GetStr(periodObj, "expression", "Expression") : null;
                assetConfigs.Add(new EngineConfigAsset
                {
                    AgentId = GetStr(aco, "agentId", "AgentId"),
                    AssetId = GetStr(aco, "assetId", "AssetId"),
                    ItemId = GetStrOrNull(aco, "itemId", "ItemId"),
                    AgentName = GetStr(aco, "agentName", "AgentName"),
                    AssetName = GetStr(aco, "assetName", "AssetName"),
                    ItemName = GetStrOrNull(aco, "itemName", "ItemName"),
                    PeriodExpression = !string.IsNullOrEmpty(periodExpression) ? periodExpression : null,
                    ConnectionInfo = (aco["connectionInfo"] ?? aco["ConnectionInfo"]) as JsonObject,
                    CollectionMethod = GetStr(aco, "collectionMethod", "CollectionMethod") is { Length: > 0 } m ? m : "ssh",
                    Collectibles = collectibles
                });
            }
        }

        var sendSchedule = GetStrOrNull(doc, "sendSchedule", "SendSchedule");
        var configSyncMins = 10;
        var minsNode = doc["configSyncPeriodMinutes"] ?? doc["ConfigSyncPeriodMinutes"];
        if (minsNode is System.Text.Json.Nodes.JsonValue jv)
        {
            try { configSyncMins = jv.GetValue<int>(); } catch { }
        }
        if (configSyncMins <= 0) configSyncMins = 10;

        return new EngineConfigSyncResult
        {
            EngineId = GetStr(doc, "engineId", "EngineId"),
            Domain = GetStr(doc, "domain", "Domain"),
            SendSchedule = !string.IsNullOrWhiteSpace(sendSchedule) ? sendSchedule : null,
            ConfigSyncPeriodMinutes = configSyncMins,
            Agents = agents,
            AssetConfigs = assetConfigs
        };
    }

    private string? GetReactorBaseUrl() => _configProvider.GetConfig()?.ServerUrl;
}
