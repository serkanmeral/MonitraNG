using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;
using Polly;
using Polly.Retry;

namespace MngDocument.Infrastructure.Clients;

/// <summary>
/// MngDataGateway dataset API istemcisi. Yazma payload'ları <see cref="object"/> (Dictionary)
/// olarak gönderilir; okuma tipli modele deserialize edilir. Bearer çağıranın token'ı ile forward.
/// </summary>
public class MngDataGatewayClient : IMngDataGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MngDataGatewayClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public MngDataGatewayClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngDataGatewayClient> logger,
        IOptions<MngDocumentSettings> settings)
    {
        _httpClient = httpClientFactory.CreateClient("MngDataGateway");
        _logger = logger;

        var baseUrl = (settings.Value.DataGateway.BaseUrl ?? "http://localhost:5010").TrimEnd('/');
        var apiVersion = settings.Value.DataGateway.ApiVersion ?? "v1";
        _httpClient.BaseAddress = new Uri($"{baseUrl}/api/{apiVersion}/");

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (_, timespan, retryCount, _) =>
                    _logger.LogWarning(
                        "Retrying MngDataGateway request. Attempt {RetryCount} after {Delay}ms",
                        retryCount, timespan.TotalMilliseconds));
    }

    public Task<T> CreateAsync<T>(string datasetName, object payload, string? token = null, CancellationToken cancellationToken = default)
        where T : class =>
        SendJsonAsync<T>(HttpMethod.Post, $"data/{datasetName}", payload, token, cancellationToken);

    public Task<T> UpdateAsync<T>(string datasetName, string id, object payload, string? token = null, CancellationToken cancellationToken = default)
        where T : class =>
        SendJsonAsync<T>(HttpMethod.Put, $"data/{datasetName}/{id}", payload, token, cancellationToken);

    public async Task<T?> GetByIdAsync<T>(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default)
        where T : class
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, $"data/{datasetName}/{id}?showHistory=true", token),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var payload = UnwrapSingleRecord(json);
        return payload is null ? null : JsonSerializer.Deserialize<T>(payload.Value.GetRawText(), JsonOptions);
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string datasetName, string? query = null, string? token = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var url = $"data/{datasetName}";
        if (!string.IsNullOrEmpty(query))
            url += $"?{query}";

        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, url, token),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var array = json.ValueKind == JsonValueKind.Array
            ? json
            : (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data
                : default);

        if (array.ValueKind != JsonValueKind.Array)
            return Array.Empty<T>();

        var list = new List<T>();
        foreach (var item in array.EnumerateArray())
        {
            var record = JsonSerializer.Deserialize<T>(item.GetRawText(), JsonOptions);
            if (record != null)
                list.Add(record);
        }

        return list;
    }

    public async Task<bool> DeleteAsync(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Delete, $"data/{datasetName}/{id}", token),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<DataGatewayPage> QueryPageAsync(
        string datasetName,
        object match,
        string? query = null,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"data/{datasetName}/query";
        if (!string.IsNullOrEmpty(query))
            url += $"?{query}";

        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Post, url, token, JsonContent.Create(new { match })),
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var items = new List<Dictionary<string, object?>>();
        if (json.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in json.EnumerateArray())
            {
                var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), JsonOptions);
                if (row != null)
                    items.Add(row);
            }
        }

        long total = items.Count;
        if (response.Headers.TryGetValues("X-Total-Count", out var values)
            && long.TryParse(values.FirstOrDefault(), out var parsed))
        {
            total = parsed;
        }

        return new DataGatewayPage(items, total);
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteNamedQueryAsync(
        string datasetName,
        string queryName,
        IReadOnlyDictionary<string, object?>? parameters = null,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters is not null)
        {
            foreach (var kv in parameters)
                payload[kv.Key] = kv.Value;
        }

        using var response = await SendWithRetryAsync(
            () => CreateRequest(
                HttpMethod.Post,
                $"data/{datasetName}/queries/{Uri.EscapeDataString(queryName)}",
                token,
                JsonContent.Create(payload)),
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array)
            return Array.Empty<Dictionary<string, object?>>();

        var list = new List<Dictionary<string, object?>>();
        foreach (var item in json.EnumerateArray())
        {
            var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), JsonOptions);
            if (row is not null)
                list.Add(row);
        }

        return list;
    }

    public async Task<byte[]> DownloadFileAsync(
        string filePath,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        var encoded = Uri.EscapeDataString(filePath);
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, $"files/download?filePath={encoded}", token),
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string url,
        object payload,
        string? token,
        CancellationToken cancellationToken) where T : class
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(method, url, token, JsonContent.Create(payload)),
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var payloadEl = UnwrapSingleRecord(json) ?? json;
        var result = JsonSerializer.Deserialize<T>(payloadEl.GetRawText(), JsonOptions);
        return result ?? throw new InvalidOperationException($"Empty response from {url}");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken) =>
        await _retryPolicy.ExecuteAsync(ct => _httpClient.SendAsync(createRequest(), ct), cancellationToken);

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"MngDataGateway {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
            null,
            response.StatusCode);
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
