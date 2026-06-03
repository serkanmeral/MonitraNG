using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Crypt;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Features.Engine.Assets;
using MngReactor.Application.Features.Query;
using MngReactor.Persistence.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Persistence.Services.Engine
{
    public class EngineProcessing : IEngineProcessing
    {
        private readonly IDataProcessing _dataProcessing;
        private readonly ICryptProcessing _cryptProcessing;
        private readonly IOptions<MngReactorSettings> _options;

        public EngineProcessing(IDataProcessing dataProcessing, ICryptProcessing cryptProcessing, IOptions<MngReactorSettings> options)
        {
            _dataProcessing = dataProcessing;
            _cryptProcessing = cryptProcessing;
            _options = options;
        }

        private async Task<JsonArray> GetEngineInfo(GetEngineAssetsQueryRequest request)
        {
            GetDataQueryRequest getDataQueryRequest = new GetDataQueryRequest
            {
                Access_Token = request.UserInfo.accessToken,
                Collection = "engines",
                Database = "mng_data_" + request.UserInfo.domain,
                Query = new JsonObject
                {
                    ["__dataId"] = request.EngineId
                }
            };

            var datas = await _dataProcessing.GetData(getDataQueryRequest);

            return datas as JsonArray;
        }

        private async Task<JsonArray> GetAssetTypes(GetEngineAssetsQueryRequest request)
        {
            GetDataQueryRequest getDataQueryRequest = new GetDataQueryRequest
            {
                Access_Token = request.UserInfo.accessToken,
                Collection = "asset_types",
                Database = "mng_common",
                Query = new JsonObject()
            };

            var datas = await _dataProcessing.GetData(getDataQueryRequest);

            return datas as JsonArray;
        }
        private async Task<JsonArray> GetAssetTypeFamilies(GetEngineAssetsQueryRequest request)
        {
            GetDataQueryRequest getDataQueryRequest = new GetDataQueryRequest
            {
                Access_Token = request.UserInfo.accessToken,
                Collection = "asset_type_families",
                Database = "mng_common",
                Query = new JsonObject()
            };

            var datas = await _dataProcessing.GetData(getDataQueryRequest);

            return datas as JsonArray;
        }
        private async Task<JsonArray> GetAssets(GetEngineAssetsQueryRequest request, JsonObject engineInfo)
        {
            JsonArray resArray = new JsonArray();

            bool isDefaultEngine = engineInfo["isDefault"].GetValue<bool>();

            JsonObject qObj = new JsonObject();

            if (isDefaultEngine)
            {
                JsonArray queryArray = new JsonArray();
                queryArray.Add(new JsonObject { ["engineId"] = request.EngineId });
                queryArray.Add(new JsonObject { ["engineId"] = string.Empty });
                qObj["$or"] = queryArray;
            }
            else
            {
                qObj["engineId"] = request.EngineId;
            }

            GetDataQueryRequest getDataQueryRequest = new GetDataQueryRequest
            {
                Access_Token = request.UserInfo.accessToken,
                Collection = "assets",
                Database = "mng_data_" + request.UserInfo.domain,
                Query = qObj
            };

            var datas = await _dataProcessing.GetData(getDataQueryRequest);

            return datas as JsonArray;
        }

        private JsonArray EnrichAssets(JsonArray assets, JsonArray assetTypes, JsonArray assetTypeFamilies)
        {
            foreach (var asset in assets) {
                string assetTypeId = asset["asset_type"].GetValue<string>();
                JsonObject assetType = assetTypes.Where(a => a["__dataId"].GetValue<string>().Equals(assetTypeId)).FirstOrDefault() as JsonObject;

                if (assetType != null)
                {
                    string assetTypeFamilyId = assetType["family"].GetValue<string>();
                    JsonObject assetTypeFamily = assetTypeFamilies.Where(a => a["__dataId"].GetValue<string>().Equals(assetTypeFamilyId)).FirstOrDefault() as JsonObject;
                    assetType["familyObj"] = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(assetTypeFamily));
                }

                asset["assetTypeObj"] = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(assetType));

            }
            return assets;
        }

        private JsonArray RebuildAssets(JsonArray assets)
        {
            JsonArray assetsArray = new JsonArray();
            foreach (var asset in assets)
            {
                JsonObject assetObj = new JsonObject();
                assetObj["assetId"] = asset["__dataId"].GetValue<string>();
                assetObj["asset_name"] = asset["asset_name"].GetValue<string>();
                assetObj["assetType"] = asset["assetTypeObj"]["type"].GetValue<string>();
                assetObj["assetFamily"] = asset["assetTypeObj"]["familyObj"]["name"].GetValue<string>();
                assetObj["connectionInfo"] = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(asset["connectionInfo"]));
                assetObj["collectibles"] = JsonSerializer.Deserialize<JsonArray>(JsonSerializer.Serialize(asset["assetTypeObj"]["collectibles"]));

                assetsArray.Add(assetObj);
            }
            return assetsArray;
        }

        public async Task<JsonArray> GetEngineAssetList(GetEngineAssetsQueryRequest request)
        {
            JsonArray assets = new JsonArray();
            JsonArray assetTypes = new JsonArray();
            JsonArray assetTypeFamilies = new JsonArray();

            var engineInfo = await GetEngineInfo(request);

            if (engineInfo.Count > 0)
            {
                assets = await GetAssets(request, engineInfo[0] as JsonObject);
                assetTypes = await GetAssetTypes(request);
                assetTypeFamilies = await GetAssetTypeFamilies(request);

                assets = EnrichAssets(assets, assetTypes, assetTypeFamilies);
            }

            return RebuildAssets(assets);
        }

        public async Task<JsonObject> GetEngineAssetData(GetEngineAssetsQueryRequest request)
        {
            JsonObject data = new JsonObject();

            var assetList = await GetEngineAssetList(request);

            var cmpBytes = await _cryptProcessing.Compress(JsonSerializer.Serialize(assetList));
            var cmpText = Convert.ToBase64String(cmpBytes);
            var dcpBytes = Convert.FromBase64String(cmpText);
            var dcpText = await _cryptProcessing.DeCompress(dcpBytes);

            return new JsonObject
            {
                ["engineId"] = request.EngineId,
                ["data"] = cmpText
            };
        }

        public async Task<string> CreateEngineConfigText(GetEngineAssetsQueryRequest request)
        {
            var engineInfo = await GetEngineInfo(request);

            //var aaa = await _cryptProcessing.Encrypt("!2345qawsedrf");
            //_logger.Information($"Encrypt : {aaa}");

            //var vbb = await _cryptProcessing.Decrypt(aaa);
            //_logger.Information($"Decrypt : {vbb}");



            JsonObject engineData = new JsonObject();
            engineData["engineId"] = engineInfo[0]["__dataId"].GetValue<string>();
            engineData["name"] = engineInfo[0]["name"].GetValue<string>();
            engineData["domain"] = engineInfo[0]["domain"].GetValue<string>();

            engineData["assetsGettingPolicy"] = engineInfo[0]["assetsGettingPolicy"].GetValue<string>();
            engineData["http_username"] = engineInfo[0]["http_username"].GetValue<string>();
            engineData["http_password"] = engineInfo[0]["http_password"].GetValue<string>();

            engineData["collectIntervalSegment"] = engineInfo[0]["collectIntervalSegment"].GetValue<string>();
            engineData["collectIntervalValue"] = engineInfo[0]["collectIntervalValue"].GetValue<int>();
            engineData["host"] = _options.Value.TokenService;

            var engineDataBytes = await _cryptProcessing.Compress(JsonSerializer.Serialize(engineData));

            JsonObject data = new JsonObject();
            data["CompressPbk"] = await _cryptProcessing.Encrypt(_options.Value.CompressPbk);
            data["CompressPrk"] = await _cryptProcessing.Encrypt(_options.Value.CompressPrk);
            data["EngineInfo"] = Convert.ToBase64String(engineDataBytes);

            var cmpBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));//await _cryptProcessing.Compress(JsonSerializer.Serialize(data));
            var cmpText = Convert.ToBase64String(cmpBytes);



            return cmpText;
        }
    }
}
