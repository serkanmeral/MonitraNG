using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Collector.Common;
using MngEngine.Application.Collector.HttpHost;
using MngEngine.Application.Collector.LinuxHost;
using MngEngine.Application.Collector.SnmpHost;
using MngEngine.Application.Collector.WindowsHost;
using MngEngine.Application.Interfaces;
using MngEngine.Domain.Entities.Asset;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngEngine.Persistence.Service.Asset
{
    public class AssetService : IAssetService
    {
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;

        public AssetService(Serilog.ILogger logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public Task<List<AssetInfo>> GetAssetsAsync()
        {
            if (_cache.TryGetValue("engineAssets", out object? engineAssetsObj) && engineAssetsObj is System.Text.Json.Nodes.JsonArray engineAssets)
            {
                var requests = BuildRequestsFromEngineAssets(engineAssets);
                var assets = requests.Select(r => r.Asset).ToList();
                return Task.FromResult(assets);
            }
            return Task.FromResult(new List<AssetInfo>());
        }

        public async Task<List<BaseCollectorRequest>> GetCollectorRequests(string? periodExpression = null)
        {
            if (_cache.TryGetValue("engineAssets", out object? engineAssetsObj) && engineAssetsObj is System.Text.Json.Nodes.JsonArray engineAssets)
            {
                var reqs = BuildRequestsFromEngineAssets(engineAssets, periodExpression);
                if (engineAssets.Count > 0 && reqs.Count == 0)
                    _logger.Warning("engineAssets count={CacheCount} ancak BuildRequests sonucu 0 (ConnectionInfo/PeriodExpression veya format sorunu?)", engineAssets.Count);
                return reqs;
            }
            return [];
        }

        private static string? GetStr(System.Text.Json.Nodes.JsonNode? n)
        {
            if (n == null) return null;
            if (n is System.Text.Json.Nodes.JsonValue jv) return jv.GetValue<string>();
            if (n is System.Text.Json.Nodes.JsonObject jo && (jo["__dataId"] ?? jo["$oid"]) is System.Text.Json.Nodes.JsonValue jv2) return jv2.GetValue<string>();
            return n.ToString();
        }

        private static int GetInt(System.Text.Json.Nodes.JsonNode? n, int def = 0)
        {
            if (n == null) return def;
            if (n is System.Text.Json.Nodes.JsonValue jv) return jv.GetValue<int>();
            return def;
        }

        private static bool PeriodMatches(string? assetPeriod, string? filterPeriod)
        {
            if (string.IsNullOrEmpty(filterPeriod)) return true;
            var ap = (assetPeriod ?? "").Trim();
            var fp = (filterPeriod ?? "").Trim();
            return string.Equals(ap, fp, StringComparison.Ordinal);
        }

        private List<BaseCollectorRequest> BuildRequestsFromEngineAssets(System.Text.Json.Nodes.JsonArray engineAssets, string? periodFilter = null)
        {
            var reqList = new List<BaseCollectorRequest>();
            foreach (var item in engineAssets)
            {
                if (item is not System.Text.Json.Nodes.JsonObject jo) continue;
                var assetPeriod = GetStr(jo["PeriodExpression"]) ?? GetStr(jo["periodExpression"]);
                if (!PeriodMatches(assetPeriod, periodFilter)) continue;

                var assetId = GetStr(jo["Asset_Id"]) ?? GetStr(jo["assetId"]) ?? "";
                var agentId = GetStr(jo["AgentId"]) ?? GetStr(jo["agentId"]) ?? "";
                var method = (GetStr(jo["CollectionMethod"]) ?? GetStr(jo["collectionMethod"]) ?? "ssh").ToLowerInvariant();
                var connInfo = (jo["ConnectionInfo"] ?? jo["connectionInfo"]) as System.Text.Json.Nodes.JsonObject;
                if (string.IsNullOrEmpty(assetId)) continue;
                connInfo ??= new System.Text.Json.Nodes.JsonObject();

                var endpoint = connInfo["endpoint"] as System.Text.Json.Nodes.JsonObject;
                var address = GetStr(connInfo["address"]) ?? GetStr(connInfo["Address"]) ?? GetStr(connInfo["host"]) ?? GetStr(connInfo["Host"])
                    ?? GetStr(endpoint?["host"]) ?? GetStr(endpoint?["Host"]) ?? "";
                var userName = GetStr(connInfo["userName"]) ?? GetStr(connInfo["UserName"]) ?? GetStr(connInfo["username"]) ?? "";
                var password = GetStr(connInfo["password"]) ?? GetStr(connInfo["Password"]) ?? "";
                var community = GetStr(connInfo["community"]) ?? GetStr(connInfo["Community"]) ?? password ?? "public";
                var port = GetInt(connInfo["port"], 0);
                if (port == 0) port = GetInt(connInfo["Port"], 0);
                if (port == 0 && endpoint != null) port = GetInt(endpoint["port"], 0);
                if (port == 0 && endpoint != null) port = GetInt(endpoint["Port"], 0);

                var collectibles = new List<Collectible>();
                var collArr = (jo["Collectibles"] ?? jo["collectibles"]) as System.Text.Json.Nodes.JsonArray;
                if (collArr != null)
                {
                    foreach (var c in collArr)
                    {
                        if (c is not System.Text.Json.Nodes.JsonObject co) continue;
                        var code = GetStr(co["code"]) ?? GetStr(co["Code"]) ?? "";
                        if (string.IsNullOrEmpty(code)) continue;
                        collectibles.Add(new Collectible { Code = code, Name = code, CType = code, Options = [] });
                    }
                }
                if (collectibles.Count == 0) collectibles.Add(new Collectible { Code = "heartbeat", Name = "Heartbeat", CType = "heartbeat", Options = [] });

                var asset = new AssetInfo
                {
                    Asset_Id = assetId,
                    Asset_Name = assetId,
                    Domain = "",
                    ParentId = "",
                    Asset_Type_Id = "",
                    Asset_Type_Name = "OsHost",
                    Asset_Sub_Type_Id = method == "wmi" ? "ST1" : "ST2",
                    Asset_Sub_Type_Name = method == "wmi" ? "Windows" : "Linux",
                    CollectibleItems = collectibles,
                    ConnectionInfo = new ConnectionInfo { Address = address, UserName = userName ?? "", Password = password ?? "", Port = port }
                };

                if (method == "wmi")
                {
                    if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(userName))
                    {
                        _logger.Warning("Asset {AssetId} atlanıyor: WMI için ConnectionInfo.address ve ConnectionInfo.userName gerekli", assetId);
                        continue;
                    }
                    reqList.Add(new WindowsHostCollectorRequest { Asset = asset, AgentId = agentId });
                }
                else if (method == "snmp")
                {
                    if (string.IsNullOrWhiteSpace(address))
                    {
                        _logger.Warning("Asset {AssetId} atlanıyor: SNMP için ConnectionInfo.address gerekli", assetId);
                        continue;
                    }
                    var portSnmp = port > 0 ? port : 161;
                    asset.ConnectionInfo = new ConnectionInfo { Address = address, UserName = "", Password = community, Port = portSnmp };
                    reqList.Add(new SnmpCollectorRequest { Asset = asset, AgentId = agentId });
                }
                else if (method == "ssh" || string.IsNullOrEmpty(method))
                {
                    if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(userName))
                    {
                        _logger.Warning("Asset {AssetId} atlanıyor: SSH için ConnectionInfo.address ve ConnectionInfo.userName gerekli", assetId);
                        continue;
                    }
                    reqList.Add(new LinuxHostCollectorRequest { Asset = asset, AgentId = agentId });
                }
                else if (method == "http" || method == "rest")
                {
                    var baseUrl = GetStr(connInfo["baseUrl"]) ?? GetStr(connInfo["BaseUrl"]) ?? "";
                    if (string.IsNullOrWhiteSpace(baseUrl))
                    {
                        _logger.Warning("Asset {AssetId} atlanıyor: HTTP/REST için connection_info.baseUrl gerekli", assetId);
                        continue;
                    }
                    var authObj = connInfo["auth"] ?? connInfo["Auth"] as System.Text.Json.Nodes.JsonObject;
                    var authType = "none";
                    string? authUsername = null, authPassword = null, authConfigId = null;
                    if (authObj != null)
                    {
                        authType = GetStr(authObj["type"]) ?? GetStr(authObj["Type"]) ?? "none";
                        authUsername = GetStr(authObj["username"]) ?? GetStr(authObj["UserName"]);
                        authPassword = GetStr(authObj["password"]) ?? GetStr(authObj["Password"]);
                        authConfigId = GetStr(authObj["authConfigId"]) ?? GetStr(authObj["AuthConfigId"]);
                    }
                    var httpInfo = new HttpConnectionInfo
                    {
                        BaseUrl = baseUrl.TrimEnd('/'),
                        AuthType = authType,
                        Username = authUsername,
                        Password = authPassword,
                        AuthConfigId = authConfigId
                    };
                    reqList.Add(new HttpCollectorRequest { Asset = asset, AgentId = agentId, HttpConnectionInfo = httpInfo });
                }
                else
                {
                    _logger.Warning("Asset {AssetId} atlanıyor: Bilinmeyen collection method={Method}", assetId, method);
                    continue;
                }
            }
            return reqList;
        }

    }
}