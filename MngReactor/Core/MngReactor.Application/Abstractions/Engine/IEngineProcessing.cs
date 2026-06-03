using MngReactor.Application.Features.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MngReactor.Application.Abstractions.Engine
{
    public interface IEngineProcessing
    {
        Task<JsonArray> GetEngineAssetList(GetEngineAssetsQueryRequest request);
        Task<JsonObject> GetEngineAssetData(GetEngineAssetsQueryRequest request);
        Task<string> CreateEngineConfigText(GetEngineAssetsQueryRequest request);
    }
}
