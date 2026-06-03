using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Configuration;

namespace MngReactor.Persistence.Services.Engine;

public class EngineConfigSyncProcessing : IEngineConfigSync
{
    /// <summary>DataGateway expand ile ilişkiler JsonObject döner; GetValue direkt çağrılırsa JsonValue hatası oluşur.</summary>
    private static string? GetString(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue jv) return jv.GetValue<string>();
        if (node is JsonObject jo)
        {
            var id = jo["__dataId"] ?? jo["$oid"];
            if (id is JsonValue jv2) return jv2.GetValue<string>();
        }
        return node.ToString();
    }

    /// <summary>JsonObject'tan string al; name/Name gibi farkli key varyantlarini dene.</summary>
    private static string GetStr(JsonObject? obj, params string[] keys)
    {
        if (obj == null) return "";
        foreach (var key in keys)
        {
            var node = obj[key];
            if (node == null) continue;
            var s = GetString(node);
            if (!string.IsNullOrEmpty(s)) return s;
        }
        return "";
    }

    private static bool GetBool(JsonNode? node, bool defaultValue = false)
    {
        if (node == null) return defaultValue;
        if (node is JsonValue jv) return jv.GetValue<bool>();
        return defaultValue;
    }

    private static int GetInt(JsonNode? node, int defaultValue = 0)
    {
        if (node == null) return defaultValue;
        if (node is JsonValue jv) return jv.GetValue<int>();
        return defaultValue;
    }

    private readonly ILogger<EngineConfigSyncProcessing> _logger;
    private readonly IDataGatewayClient _dg;
    private readonly ICryptProcessing _cryptProcessing;
    private readonly IOptions<MngReactorSettings> _options;

    public EngineConfigSyncProcessing(
        ILogger<EngineConfigSyncProcessing> logger,
        IDataGatewayClient dg,
        ICryptProcessing cryptProcessing,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _dg = dg;
        _cryptProcessing = cryptProcessing;
        _options = options;
    }

    public async Task<EngineConfigSyncResult?> GetConfigAsync(string engineId, string domain, string accessToken, CancellationToken cancellationToken = default)
    {
        var token = ResolveToken(domain, accessToken);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("EngineConfigSync: token bulunamadı domain={Domain}", domain);
            return null;
        }

        var engine = await GetEngineAsync(engineId, token, cancellationToken);
        if (engine == null)
        {
            _logger.LogWarning("Engine {EngineId} not found in domain {Domain}", engineId, domain);
            return null;
        }

        var agents = await GetAgentsForEngineAsync(engineId, token, cancellationToken);
        var periods = await GetCollectionPeriodsAsync(token, cancellationToken);
        var schedules = await GetSchedulesAsync(token, cancellationToken);

        var result = new EngineConfigSyncResult
        {
            EngineId = engineId,
            Domain = domain,
            SendSchedule = GetStr(engine, "sendSchedule", "SendSchedule").Trim(),
            ConfigSyncPeriodMinutes = GetInt(engine["configSyncPeriodMinutes"] ?? engine["ConfigSyncPeriodMinutes"], 10)
        };
        if (string.IsNullOrEmpty(result.SendSchedule)) result.SendSchedule = null;

        foreach (var agentDoc in agents)
        {
            var agentId = GetString(agentDoc["__dataId"]) ?? "";
            var agentName = GetString(agentDoc["name"]) ?? "";
            var defaultPeriod = ResolvePeriod(agentDoc, periods);
            var defaultSchedule = ResolveSchedule(agentDoc, schedules);

            result.Agents.Add(new EngineConfigAgent
            {
                AgentId = agentId,
                Name = agentName,
                Status = GetString(agentDoc["status"]) ?? "active",
                DefaultPeriod = defaultPeriod,
                DefaultSchedule = defaultSchedule
            });

            var assetConfigs = agentDoc["asset_configs"] as JsonArray;
            if (assetConfigs == null) continue;

            foreach (var ac in assetConfigs)
            {
                var acObj = ac as JsonObject;
                if (acObj == null) continue;

                var active = GetBool(acObj["active"], true);
                var assetId = GetString(acObj["assetId"]) ?? "";
                if (string.IsNullOrEmpty(assetId)) continue;

                var asset = await GetAssetAsync(assetId, token, cancellationToken);
                if (asset == null)
                {
                    _logger.LogWarning("EngineConfigSync: Asset bulunamadi assetId={AssetId}", assetId);
                    continue;
                }

                var assetName = GetStr(asset, "name", "Name");
                var itemId = GetString(asset["itemId"]) ?? GetString(asset["ItemId"]);
                var itemName = await GetItemNameAsync(itemId, token, cancellationToken);
                _logger.LogDebug("EngineConfigSync: assetId={AssetId} assetName={AssetName} itemName={ItemName}", assetId, assetName, itemName ?? "(null)");

                var period = ResolvePeriodFromConfig(acObj, defaultPeriod, periods);
                var schedule = ResolveScheduleFromConfig(acObj, defaultSchedule, schedules);

                JsonObject? connectionInfo = null;
                if (asset["connection_info"] != null)
                {
                    connectionInfo = await DecryptConnectionInfoAsync(asset["connection_info"]!, cancellationToken);
                }

                var collectibles = BuildCollectibles(asset);
                var typeObj = asset["type"] as JsonObject;
                var collectionMethod = GetString(typeObj?["collection_method"]) ?? "ssh";

                result.AssetConfigs.Add(new EngineConfigAsset
                {
                    AgentId = agentId,
                    AssetId = assetId,
                    ItemId = itemId,
                    AgentName = agentName,
                    AssetName = assetName,
                    ItemName = itemName,
                    Period = period,
                    Schedule = schedule,
                    Active = active,
                    ConnectionInfo = connectionInfo,
                    CollectionMethod = collectionMethod,
                    Collectibles = collectibles
                });
            }
        }

        return result;
    }

    private string? ResolveToken(string domain, string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken)) return accessToken;
        return _options.Value?.DataGateway?.DomainTokens?.GetValueOrDefault(domain);
    }

    private async Task<JsonObject?> GetEngineAsync(string engineId, string token, CancellationToken ct)
    {
        return await _dg.GetByIdAsync("mon_engines", engineId, token, ct);
    }

    private async Task<List<JsonObject>> GetAgentsForEngineAsync(string engineId, string token, CancellationToken ct)
    {
        var arr = await _dg.GetListAsync("mon_agents", $"engineId:eq:{engineId}", token, 1000, ct);
        var list = new List<JsonObject>();
        foreach (var item in arr)
            if (item is JsonObject jo)
                list.Add(jo);
        return list;
    }

    private async Task<JsonObject?> GetAssetAsync(string assetId, string token, CancellationToken ct)
    {
        return await _dg.GetByIdAsync("mon_assets", assetId, token, ct);
    }

    private async Task<string?> GetItemNameAsync(string? itemId, string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        var item = await _dg.GetByIdAsync("mon_items", itemId, token, ct);
        if (item == null) return null;
        var name = GetStr(item, "name", "Name");
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private async Task<List<JsonObject>> GetCollectionPeriodsAsync(string token, CancellationToken ct)
    {
        var arr = await _dg.GetListAsync("mon_collection_periods", null, token, 100, ct);
        var list = new List<JsonObject>();
        foreach (var item in arr)
            if (item is JsonObject jo)
                list.Add(jo);
        return list;
    }

    private async Task<List<JsonObject>> GetSchedulesAsync(string token, CancellationToken ct)
    {
        var arr = await _dg.GetListAsync("mon_schedules", null, token, 100, ct);
        var list = new List<JsonObject>();
        foreach (var item in arr)
            if (item is JsonObject jo)
                list.Add(jo);
        return list;
    }

    private static JsonObject? ResolvePeriod(JsonObject agent, List<JsonObject> periods)
    {
        var periodId = GetString(agent["defaultPeriodId"]) ?? "";
        if (string.IsNullOrEmpty(periodId)) periodId = GetString(periods.FirstOrDefault()?["__dataId"]) ?? "";
        var p = periods.FirstOrDefault(x => GetString(x["__dataId"]) == periodId);
        if (p == null) return null;
        return new JsonObject { ["expression"] = GetString(p["expression"]) ?? "" };
    }

    private static JsonObject? ResolveSchedule(JsonObject agent, List<JsonObject> schedules)
    {
        var scheduleId = GetString(agent["defaultScheduleId"]) ?? "";
        if (string.IsNullOrEmpty(scheduleId)) scheduleId = GetString(schedules.FirstOrDefault()?["__dataId"]) ?? "";
        var s = schedules.FirstOrDefault(x => GetString(x["__dataId"]) == scheduleId);
        if (s == null) return new JsonObject { ["type"] = "always" };
        var type = GetString(s["type"]) ?? "always";
        var obj = new JsonObject { ["type"] = type };
        if (s["config"] != null && s["config"] is JsonObject configObj)
            obj["config"] = configObj;
        return obj;
    }

    private static JsonObject? ResolvePeriodFromConfig(JsonObject ac, JsonObject? defaultPeriod, List<JsonObject> periods)
    {
        var periodId = GetString(ac["periodId"]) ?? "";
        if (!string.IsNullOrEmpty(periodId))
        {
            var p = periods.FirstOrDefault(x => GetString(x["__dataId"]) == periodId);
            if (p != null) return new JsonObject { ["expression"] = GetString(p["expression"]) ?? "" };
        }
        return defaultPeriod;
    }

    private static JsonObject? ResolveScheduleFromConfig(JsonObject ac, JsonObject? defaultSchedule, List<JsonObject> schedules)
    {
        var scheduleId = GetString(ac["scheduleId"]) ?? "";
        if (!string.IsNullOrEmpty(scheduleId))
        {
            var s = schedules.FirstOrDefault(x => GetString(x["__dataId"]) == scheduleId);
            if (s != null)
            {
                var type = GetString(s["type"]) ?? "always";
                var obj = new JsonObject { ["type"] = type };
                if (s["config"] != null && s["config"] is JsonObject configObj)
                    obj["config"] = configObj;
                return obj;
            }
        }
        return defaultSchedule;
    }

    private async Task<JsonObject?> DecryptConnectionInfoAsync(JsonNode connInfo, CancellationToken ct)
    {
        try
        {
            if (connInfo is JsonValue jv)
            {
                var str = jv.GetValue<string>();
                if (!string.IsNullOrEmpty(str))
                {
                    var bytes = Convert.FromBase64String(str);
                    var decrypted = await _cryptProcessing.DeCompress(bytes);
                    return JsonNode.Parse(decrypted) as JsonObject;
                }
            }
            if (connInfo is JsonObject connObj)
                return connObj;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not decrypt connection_info");
        }
        return null;
    }

    private static List<EngineConfigCollectible> BuildCollectibles(JsonObject asset)
    {
        var list = new List<EngineConfigCollectible>();
        var typeObj = asset["type"] as JsonObject;
        var collectibles = typeObj?["collectibles"] as JsonArray;
        if (collectibles == null) return list;

        foreach (var c in collectibles)
        {
            var cObj = c as JsonObject;
            if (cObj == null) continue;
            list.Add(new EngineConfigCollectible
            {
                Code = GetString(cObj["code"]) ?? "",
                Enabled = GetBool(cObj["enabled"], true),
                Params = cObj["params"] as JsonObject
            });
        }
        return list;
    }
}
