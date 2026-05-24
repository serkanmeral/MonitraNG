using System.Net.Http.Headers;
using System.Net.Http.Json;
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

            var result = await response.Content.ReadFromJsonAsync<T>();
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to deserialize response from {url}");
            }

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
            {
                url += $"?{query}";
            }

            using var response = await SendWithRetryAsync(
                () => CreateRequest(HttpMethod.Get, url, token));

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<IEnumerable<T>>();
            if (result == null)
            {
                return Enumerable.Empty<T>();
            }

            _logger.LogDebug("Retrieved data from dataset {DatasetName}, Count: {Count}", datasetName, result.Count());
            return result;
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
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<T>();
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

            var result = await response.Content.ReadFromJsonAsync<T>();
            if (result == null)
            {
                throw new InvalidOperationException($"Failed to deserialize response from {url}");
            }

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
            {
                return false;
            }

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

    /// <summary>
    /// Polly may retry; each attempt needs a fresh HttpRequestMessage.
    /// </summary>
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
        {
            request.Content = content;
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
