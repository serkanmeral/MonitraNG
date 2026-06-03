using System.Text.Json.Nodes;
using MngReactor.Application.Abstractions.Data;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// Entegrasyon testlerinde gercek DG API olmadan controller'lari calistirmak icin mock.
/// </summary>
public class MockDataGatewayClient : IDataGatewayClient
{
    public Task<JsonArray> GetListAsync(string collection, string? filter, string? accessToken, int limit = 1000, CancellationToken ct = default)
        => Task.FromResult(new JsonArray());

    public Task<JsonObject?> GetByIdAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default)
        => Task.FromResult<JsonObject?>(null);

    public Task<JsonArray> AggregateAsync(string collection, JsonArray pipeline, string? accessToken, CancellationToken ct = default)
        => Task.FromResult(new JsonArray());

    public Task<JsonObject> CreateAsync(string collection, JsonObject data, string? accessToken, CancellationToken ct = default)
    {
        var id = "mock-id-" + Guid.NewGuid().ToString("N")[..8];
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
        => Task.FromResult(true);

    public Task<bool> DeleteAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default)
        => Task.FromResult(true);
}
