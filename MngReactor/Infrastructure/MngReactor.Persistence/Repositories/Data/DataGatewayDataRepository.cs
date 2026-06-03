using System.Text.Json.Nodes;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Features.Command.Data;
using MngReactor.Application.Repositories.Data;
using MngReactor.Application.Features.Query;

namespace MngReactor.Persistence.Repositories.Data;

/// <summary>
/// DataRepository implementasyonu - tum veri islemleri DataGateway HTTP API uzerinden.
/// </summary>
public class DataGatewayDataRepository : IDataRepository
{
    private readonly IDataGatewayClient _dg;

    public DataGatewayDataRepository(IDataGatewayClient dg)
    {
        _dg = dg;
    }

    public async Task<JsonNode> GetData(GetDataQueryRequest request)
    {
        var filter = BuildFilter(request.Query);
        return await _dg.GetListAsync(request.Collection, filter, request.Access_Token, 1000);
    }

    public async Task<JsonNode> InsertData(DataCommandRequest request)
    {
        if (request.Data is not JsonObject data)
            return new JsonObject { ["isSuccess"] = false, ["errorMessage"] = "Unsupported JSON type" };

        try
        {
            var res = await _dg.CreateAsync(request.Collection, data, request.Access_Token);
            var success = res["success"]?.GetValue<bool>() ?? res["Success"]?.GetValue<bool>() ?? false;
            var dgData = res["data"] ?? res["Data"];
            var id = dgData?["__dataId"]?.GetValue<string>();
            return new JsonObject
            {
                ["isSuccess"] = success,
                ["__dataId"] = string.IsNullOrEmpty(id) ? new JsonArray() : new JsonArray(JsonValue.Create(id!)),
                ["operation"] = "insert",
                ["collection"] = request.Collection,
                ["db"] = request.Database
            };
        }
        catch (Exception ex)
        {
            return new JsonObject { ["isSuccess"] = false, ["errorMessage"] = ex.Message };
        }
    }

    public async Task<JsonNode> UpdateData(DataCommandRequest request)
    {
        var dataId = request.Data?["__dataId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(dataId))
            return new JsonObject { ["isSuccess"] = false, ["errorMessage"] = "Missing __dataId" };

        if (request.Data is not JsonObject data)
            return new JsonObject { ["isSuccess"] = false, ["errorMessage"] = "Invalid data" };

        var ok = await _dg.UpdateAsync(request.Collection, dataId, data, request.Access_Token);
        return new JsonObject
        {
            ["isSuccess"] = ok,
            ["operation"] = "update",
            ["collection"] = request.Collection,
            ["db"] = request.Database,
            ["__dataId"] = dataId
        };
    }

    public async Task<JsonNode> DeleteData(DataCommandRequest request)
    {
        var dataId = request.Data?["__dataId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(dataId))
            return new JsonObject { ["isSuccess"] = false, ["errorMessage"] = "Missing __dataId" };

        var ok = await _dg.DeleteAsync(request.Collection, dataId, request.Access_Token);
        return new JsonObject
        {
            ["isSuccess"] = ok,
            ["operation"] = "delete",
            ["collection"] = request.Collection,
            ["db"] = request.Database,
            ["__dataId"] = dataId
        };
    }

    private static string? BuildFilter(JsonObject? query)
    {
        if (query == null || query.Count == 0) return null;
        var parts = new List<string>();
        foreach (var prop in query)
        {
            if (prop.Value == null) continue;
            var v = prop.Value.ToString().Trim('"');
            parts.Add($"{prop.Key}:eq:{v}");
        }
        return parts.Count == 0 ? null : string.Join(",", parts);
    }
}
