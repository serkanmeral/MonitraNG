using System.Text.Json.Nodes;
using MngReactor.Application.Abstractions.Data;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// IDataGatewayClient mock - Create/Update gonderilen payload'i yakalar.
/// connection_info sifreleme testleri icin kullanilir.
/// </summary>
public class CapturingMockDataGatewayClient : IDataGatewayClient
{
    public JsonObject? LastCreatePayload { get; set; }
    public JsonObject? LastUpdatePayload { get; set; }
    public string? LastUpdateDataId { get; private set; }

    public void Reset()
    {
        LastCreatePayload = null;
        LastUpdatePayload = null;
        LastUpdateDataId = null;
    }

    public Task<JsonArray> GetListAsync(string collection, string? filter, string? accessToken, int limit = 1000, CancellationToken ct = default)
        => Task.FromResult(new JsonArray());

    public Task<JsonObject?> GetByIdAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default)
        => Task.FromResult<JsonObject?>(null);

    public Task<JsonArray> AggregateAsync(string collection, JsonArray pipeline, string? accessToken, CancellationToken ct = default)
        => Task.FromResult(new JsonArray());

    public Task<JsonObject> CreateAsync(string collection, JsonObject data, string? accessToken, CancellationToken ct = default)
    {
        LastCreatePayload = CloneJson(data);
        var id = "capture-id-" + Guid.NewGuid().ToString("N")[..8];
        var dataObj = new JsonObject { ["__dataId"] = id };
        return Task.FromResult(new JsonObject
        {
            ["success"] = true,
            ["Success"] = true,
            ["data"] = dataObj,
            ["Data"] = dataObj
        });
    }

    public Task<JsonObject> BulkCreateAsync(string collection, JsonArray items, string? accessToken, CancellationToken ct = default)
        => Task.FromResult(new JsonObject { ["success"] = true, ["Success"] = true });

    public Task<bool> UpdateAsync(string collection, string dataId, JsonObject data, string? accessToken, CancellationToken ct = default, bool skipEventPublish = false)
    {
        LastUpdatePayload = CloneJson(data);
        LastUpdateDataId = dataId;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default)
        => Task.FromResult(true);

    private static JsonObject CloneJson(JsonObject src)
    {
        return JsonNode.Parse(src.ToJsonString()) as JsonObject ?? new JsonObject();
    }
}
