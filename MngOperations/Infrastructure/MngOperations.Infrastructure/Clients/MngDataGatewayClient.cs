using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Diagnostics;
using MngOperations.Application.Interfaces;
using Polly;
using Polly.Retry;

namespace MngOperations.Infrastructure.Clients;

public class MngDataGatewayClient : IMngDataGatewayClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MngDataGatewayClient> _logger;
    private readonly MngOperationsSettings _settings;
    private readonly OcCallStats _stats;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public MngDataGatewayClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngDataGatewayClient> logger,
        IOptions<MngOperationsSettings> settings,
        OcCallStats stats)
    {
        _httpClient = httpClientFactory.CreateClient("MngDataGateway");
        _logger = logger;
        _settings = settings.Value;
        _stats = stats;

        var baseUrl = (_settings.DataGateway.BaseUrl ?? "http://localhost:5010").TrimEnd('/');
        var apiVersion = _settings.DataGateway.ApiVersion ?? "v1";
        _httpClient.BaseAddress = new Uri($"{baseUrl}/api/{apiVersion}/");

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (int)r.StatusCode >= 500)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (outcome, timespan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        "Retrying MngDataGateway request. Attempt {RetryCount} after {Delay}ms",
                        retryCount,
                        timespan.TotalMilliseconds);
                });
    }

    public Task<T> CreateAsync<T>(string datasetName, T data, string? token = null, CancellationToken cancellationToken = default)
        where T : class =>
        SendJsonAsync<T>(HttpMethod.Post, $"data/{datasetName}", data, $"create:{datasetName}", token, cancellationToken);

    public async Task<IEnumerable<T>> GetAsync<T>(string datasetName, string? query = null, string? token = null, CancellationToken cancellationToken = default)
        where T : class
    {
        var url = $"data/{datasetName}";
        if (!string.IsNullOrEmpty(query))
            url += $"?{query}";

        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, url, token),
            $"get:{datasetName}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array)
            return Enumerable.Empty<T>();

        var list = new List<T>();
        foreach (var item in json.EnumerateArray())
        {
            var normalized = CollapseExpandedRelations(item);
            var record = JsonSerializer.Deserialize<T>(normalized.GetRawText(), JsonOptions);
            if (record != null)
                list.Add(record);
        }

        return list;
    }

    public async Task<T?> GetByIdAsync<T>(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default, bool expand = true)
        where T : class
    {
        // DG GetById varsayılanı expand=true; expand=false ile relation'lar ham id kalır
        // (örn. labels op_labels'a expand edilip düşürülmesin — MO kendi op_tags'tan çözer).
        var path = expand ? $"data/{datasetName}/{id}" : $"data/{datasetName}/{id}?expand=false";
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, path, token),
            $"getById:{datasetName}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var payload = UnwrapSingleRecord(json);
        if (payload == null)
            return null;

        var normalized = CollapseExpandedRelations(payload.Value);
        return JsonSerializer.Deserialize<T>(normalized.GetRawText(), JsonOptions);
    }

    private static JsonElement CollapseExpandedRelations(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return element;

        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = CollapseRelationValue(prop.Name, prop.Value);
        }

        return JsonSerializer.SerializeToElement(dict, JsonOptions);
    }

    // İsmi "Id"/"Ids" ile bitmeyen ama yine de relation olan çekirdek alanlar.
    // DG tek-kayıt okumada bunları object olarak expand edebilir; id'ye indirgenmezse
    // patch/transition `merged = existing`'i geri yazarken object persist edilir (alan bozulur).
    private static readonly HashSet<string> SingleRelationFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "assignee"
    };

    private static readonly HashSet<string> MultiRelationFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "watchers"
    };

    private static JsonElement CollapseRelationValue(string propertyName, JsonElement value)
    {
        var isIdsArray = propertyName.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
        var isMultiRelation = MultiRelationFieldNames.Contains(propertyName);
        if ((isIdsArray || isMultiRelation) && value.ValueKind == JsonValueKind.Array)
        {
            var ids = new List<string?>();
            foreach (var item in value.EnumerateArray())
            {
                ids.Add(ExtractRelationId(item));
            }

            return JsonSerializer.SerializeToElement(ids, JsonOptions);
        }

        if (propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || SingleRelationFieldNames.Contains(propertyName))
        {
            var id = ExtractRelationId(value);
            if (id != null)
                return JsonSerializer.SerializeToElement(id, JsonOptions);
        }

        return value;
    }

    private static string? ExtractRelationId(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object when value.TryGetProperty("__dataId", out var dataId) => dataId.GetString(),
            JsonValueKind.Object when value.TryGetProperty("dataId", out var altId) => altId.GetString(),
            _ => null
        };

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

    public Task<T> UpdateAsync<T>(string datasetName, string id, T data, string? token = null, CancellationToken cancellationToken = default)
        where T : class =>
        SendJsonAsync<T>(HttpMethod.Put, $"data/{datasetName}/{id}", data, $"update:{datasetName}", token, cancellationToken);

    public async Task<bool> DeleteAsync(string datasetName, string id, string? token = null, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Delete, $"data/{datasetName}/{id}", token),
            $"delete:{datasetName}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ExecuteQueryAsync(
        string datasetName,
        string queryName,
        Dictionary<string, object?> parameters,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"data/{datasetName}/queries/{queryName}";
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Post, url, token, JsonContent.Create(parameters)),
            $"execQuery:{datasetName}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"MngDataGateway {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array)
            return Array.Empty<Dictionary<string, object?>>();

        var list = new List<Dictionary<string, object?>>();
        foreach (var item in json.EnumerateArray())
        {
            var row = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), JsonOptions);
            if (row != null)
                list.Add(row);
        }

        return list;
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
            $"query:{datasetName}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"MngDataGateway {(int)response.StatusCode} {response.ReasonPhrase}: {error}",
                null,
                response.StatusCode);
        }

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

    private async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string url,
        T data,
        string op,
        string? token,
        CancellationToken cancellationToken) where T : class
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(method, url, token, JsonContent.Create(data)),
            op,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"MngDataGateway {(int)response.StatusCode} {response.ReasonPhrase}: {body}",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        var payload = UnwrapSingleRecord(json) ?? json;
        var normalized = CollapseExpandedRelations(payload);
        var result = JsonSerializer.Deserialize<T>(normalized.GetRawText(), JsonOptions);
        return result ?? throw new InvalidOperationException($"Empty response from {url}");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        string op,
        CancellationToken cancellationToken)
    {
        // GEÇİCİ (perf/oc-optimization): istek başına DG çağrı sayısı/süresi.
        var sw = Stopwatch.StartNew();
        try
        {
            return await _retryPolicy.ExecuteAsync(ct => _httpClient.SendAsync(createRequest(), ct), cancellationToken);
        }
        finally
        {
            sw.Stop();
            _stats.RecordDg(op, sw.ElapsedMilliseconds);
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
