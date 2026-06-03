using System.Text.Json.Nodes;

namespace MngReactor.Application.Abstractions.Data;

/// <summary>
/// DataGateway HTTP API client - tum veri islemleri DG uzerinden.
/// MngReactor dogrudan MongoDB'ye erisim yapmaz.
/// </summary>
public interface IDataGatewayClient
{
    Task<JsonArray> GetListAsync(string collection, string? filter, string? accessToken, int limit = 1000, CancellationToken ct = default);
    Task<JsonObject?> GetByIdAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default);
    Task<JsonArray> AggregateAsync(string collection, JsonArray pipeline, string? accessToken, CancellationToken ct = default);
    Task<JsonObject> CreateAsync(string collection, JsonObject data, string? accessToken, CancellationToken ct = default);
    Task<JsonObject> BulkCreateAsync(string collection, JsonArray items, string? accessToken, CancellationToken ct = default);
    Task<bool> UpdateAsync(string collection, string dataId, JsonObject data, string? accessToken, CancellationToken ct = default, bool skipEventPublish = false);
    Task<bool> DeleteAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default);
}
