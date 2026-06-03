using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Data;
using MngReactor.Application.Configuration;

namespace MngReactor.Persistence.Services.Data;

public class DataGatewayClient : IDataGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly MngReactorSettings _settings;
    private readonly ILogger<DataGatewayClient> _logger;

    public DataGatewayClient(HttpClient httpClient, IOptions<MngReactorSettings> options, ILogger<DataGatewayClient> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    private string BaseUrl => _settings.DataGateway?.BaseUrl?.TrimEnd('/') ?? "http://localhost:5010";
    private string ApiVersion => _settings.DataGateway?.ApiVersion ?? "v1";

    private void SetAuth(HttpRequestMessage req, string? token)
    {
        if (!string.IsNullOrEmpty(token))
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
    }

    public async Task<JsonArray> GetListAsync(string collection, string? filter, string? accessToken, int limit = 1000, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}?limit={limit}";
        if (!string.IsNullOrEmpty(filter)) url += "&filter=" + Uri.EscapeDataString(filter);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuth(req, accessToken);

        try
        {
            var res = await _httpClient.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("DG GetList failed: {Status} {Url}", res.StatusCode, url);
                return new JsonArray();
            }
            var data = JsonNode.Parse(body);
            return data is JsonArray arr ? arr : new JsonArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DG GetList failed: {Url}", url);
            return new JsonArray();
        }
    }

    public async Task<JsonObject?> GetByIdAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}/{dataId}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetAuth(req, accessToken);

        try
        {
            var res = await _httpClient.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) return null;
            var data = JsonNode.Parse(body);
            if (data is JsonArray arr && arr.Count > 0)
                return arr[0] as JsonObject;
            _logger.LogWarning("DG GetById unexpected format: collection={Collection} id={Id} isArray={IsArr} bodyLen={Len}",
                collection, dataId, data is JsonArray, body?.Length ?? 0);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DG GetById failed: {Url}", url);
            return null;
        }
    }

    public async Task<JsonArray> AggregateAsync(string collection, JsonArray pipeline, string? accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}/aggregate";
        var body = new JsonObject { ["pipeline"] = pipeline };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        SetAuth(req, accessToken);

        try
        {
            var res = await _httpClient.SendAsync(req, ct);
            var resBody = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("DG Aggregate failed: {Status} {Url}", res.StatusCode, url);
                return new JsonArray();
            }
            var data = JsonNode.Parse(resBody);
            return data is JsonArray arr ? arr : new JsonArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DG Aggregate failed: {Url}", url);
            return new JsonArray();
        }
    }

    public async Task<JsonObject> CreateAsync(string collection, JsonObject data, string? accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(data.ToJsonString(), Encoding.UTF8, "application/json")
        };
        SetAuth(req, accessToken);

        var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        return (JsonNode.Parse(body) as JsonObject) ?? new JsonObject();
    }

    public async Task<JsonObject> BulkCreateAsync(string collection, JsonArray items, string? accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}/bulk";
        var body = new JsonObject { ["items"] = items };
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        SetAuth(req, accessToken);

        var res = await _httpClient.SendAsync(req, ct);
        var resBody = await res.Content.ReadAsStringAsync(ct);
        return (JsonNode.Parse(resBody) as JsonObject) ?? new JsonObject();
    }

    public async Task<bool> UpdateAsync(string collection, string dataId, JsonObject data, string? accessToken, CancellationToken ct = default, bool skipEventPublish = false)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}/{dataId}";
        if (skipEventPublish)
            url += "?skipEventPublish=true";
        using var req = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(data.ToJsonString(), Encoding.UTF8, "application/json")
        };
        SetAuth(req, accessToken);

        var res = await _httpClient.SendAsync(req, ct);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string collection, string dataId, string? accessToken, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/{ApiVersion}/data/{collection}/{dataId}";
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        SetAuth(req, accessToken);

        var res = await _httpClient.SendAsync(req, ct);
        return res.IsSuccessStatusCode;
    }
}
