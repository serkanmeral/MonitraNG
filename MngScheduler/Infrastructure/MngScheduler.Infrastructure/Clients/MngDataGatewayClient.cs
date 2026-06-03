using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Clients;

/// <summary>
/// HttpClient wrapper for MngDataGateway dataset API
/// Used for User Job CRUD operations
/// </summary>
public class MngDataGatewayClient : IMngDataGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MngDataGatewayClient> _logger;
    private readonly MngSchedulerSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public MngDataGatewayClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngDataGatewayClient> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _httpClient = httpClientFactory.CreateClient("MngDataGateway");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));

        var baseUrl = _settings.DataGateway.BaseUrl ?? "http://localhost:5070";
        var apiVersion = _settings.DataGateway.ApiVersion ?? "v1";
        _httpClient.BaseAddress = new Uri($"{baseUrl}/api/{apiVersion}/");

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retrying MngDataGateway request. Attempt {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds);
                });
    }

    public async Task<T> CreateAsync<T>(string datasetName, T data, string? token = null) where T : class
    {
        try
        {
            var url = $"data/{datasetName}";
            using var response = await SendWithRetryAsync(
                () => CreateRequest(HttpMethod.Post, url, token, JsonContent.Create(data)));

            response.EnsureSuccessStatusCode();
            var result = await DeserializeAsync<T>(response);
            _logger.LogDebug("Created data in dataset {DatasetName}", datasetName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating data in dataset {DatasetName}", datasetName);
            throw;
        }
    }

    public async Task<IEnumerable<T>> GetAsync<T>(string datasetName, string? query = null, string? token = null) where T : class
    {
        try
        {
            var url = $"data/{datasetName}";
            if (!string.IsNullOrEmpty(query))
                url += $"?{query}";

            using var response = await SendWithRetryAsync(
                () => CreateRequest(HttpMethod.Get, url, token));

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Dataset {DatasetName} not found while listing data — returning empty result",
                    datasetName);
                return Enumerable.Empty<T>();
            }

            response.EnsureSuccessStatusCode();
            var list = await DeserializeListAsync<T>(response);
            _logger.LogDebug("Retrieved data from dataset {DatasetName}, Count: {Count}", datasetName, list.Count);
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data from dataset {DatasetName}", datasetName);
            throw;
        }
    }

    public async Task<T?> GetByIdAsync<T>(string datasetName, string id, string? token = null) where T : class
    {
        try
        {
            var url = $"data/{datasetName}/{id}";
            using var response = await SendWithRetryAsync(
                () => CreateRequest(HttpMethod.Get, url, token));

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var result = await DeserializeAsync<T>(response);
            _logger.LogDebug("Retrieved data by ID from dataset {DatasetName}, Id: {Id}", datasetName, id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data by ID from dataset {DatasetName}, Id: {Id}", datasetName, id);
            throw;
        }
    }

    public async Task<T> UpdateAsync<T>(string datasetName, string id, T data, string? token = null) where T : class
    {
        try
        {
            var url = $"data/{datasetName}/{id}";
            using var response = await SendWithRetryAsync(
                () => CreateRequest(HttpMethod.Put, url, token, JsonContent.Create(data)));

            response.EnsureSuccessStatusCode();
            var result = await DeserializeAsync<T>(response);
            _logger.LogDebug("Updated data in dataset {DatasetName}, Id: {Id}", datasetName, id);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating data in dataset {DatasetName}, Id: {Id}", datasetName, id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string datasetName, string id, string? token = null)
    {
        try
        {
            var url = $"data/{datasetName}/{id}";
            using var response = await SendWithRetryAsync(
                () => CreateRequest(HttpMethod.Delete, url, token));

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Deleted data from dataset {DatasetName}, Id: {Id}", datasetName, id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data from dataset {DatasetName}, Id: {Id}", datasetName, id);
            throw;
        }
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response) where T : class
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var payload = UnwrapSingleRecord(json) ?? json;
        var result = JsonSerializer.Deserialize<T>(payload.GetRawText(), JsonOptions);
        return result ?? throw new InvalidOperationException("Failed to deserialize MngDataGateway response.");
    }

    private static async Task<List<T>> DeserializeListAsync<T>(HttpResponseMessage response) where T : class
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = UnwrapList(json);
        var list = new List<T>();
        foreach (var item in items)
        {
            var record = JsonSerializer.Deserialize<T>(item.GetRawText(), JsonOptions);
            if (record != null)
                list.Add(record);
        }

        return list;
    }

    private static IEnumerable<JsonElement> UnwrapList(JsonElement json)
    {
        switch (json.ValueKind)
        {
            case JsonValueKind.Array:
                return json.EnumerateArray().ToList();
            case JsonValueKind.Object when json.TryGetProperty("data", out var data):
                return data.ValueKind switch
                {
                    JsonValueKind.Array => data.EnumerateArray().ToList(),
                    JsonValueKind.Object => new[] { data },
                    _ => Array.Empty<JsonElement>()
                };
            default:
                return Array.Empty<JsonElement>();
        }
    }

    private static JsonElement? UnwrapSingleRecord(JsonElement json)
    {
        switch (json.ValueKind)
        {
            case JsonValueKind.Array:
            {
                using var enumerator = json.EnumerateArray();
                return enumerator.MoveNext() ? enumerator.Current : null;
            }
            case JsonValueKind.Object when json.TryGetProperty("data", out var data):
                return data.ValueKind switch
                {
                    JsonValueKind.Array => UnwrapSingleRecord(data),
                    JsonValueKind.Object => data,
                    _ => null
                };
            case JsonValueKind.Object:
                return json;
            default:
                return null;
        }
    }

    private Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> createRequest) =>
        _retryPolicy.ExecuteAsync(() => _httpClient.SendAsync(createRequest()));

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        string? token,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (content != null)
            request.Content = content;

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}
