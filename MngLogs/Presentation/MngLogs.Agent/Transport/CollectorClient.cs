using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Transport;

public interface ICollectorClient
{
    Task<IngestBatchResponse?> SendBatchAsync(IngestBatchRequest request, CancellationToken cancellationToken = default);
    Task<bool> HealthAsync(CancellationToken cancellationToken = default);
    Task<EventLogPackageCatalogPullResult> GetEventLogPackageCatalogAsync(
        string? ifNoneMatchVersion = null,
        CancellationToken cancellationToken = default);
}

public sealed class CollectorClient : ICollectorClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAgentConfigStore _config;

    public CollectorClient(IHttpClientFactory httpClientFactory, IAgentConfigStore config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<bool> HealthAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeBase(_config.Current.System.CollectorBaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient("collector");
            using var response = await client.GetAsync($"{baseUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IngestBatchResponse?> SendBatchAsync(
        IngestBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = _config.Current.System;
        var baseUrl = NormalizeBase(settings.CollectorBaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        var client = _httpClientFactory.CreateClient("collector");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/ingest/batches")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            message.Headers.TryAddWithoutValidation("X-MngLogs-ApiKey", settings.ApiKey);

        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<IngestBatchResponse>(JsonOptions, cancellationToken);
    }

    public async Task<EventLogPackageCatalogPullResult> GetEventLogPackageCatalogAsync(
        string? ifNoneMatchVersion = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _config.Current.System;
        var baseUrl = NormalizeBase(settings.CollectorBaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return EventLogPackageCatalogPullResult.Failed();

        var client = _httpClientFactory.CreateClient("collector");
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/policy/eventlog-packages");

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            message.Headers.TryAddWithoutValidation("X-MngLogs-ApiKey", settings.ApiKey);

        if (!string.IsNullOrWhiteSpace(ifNoneMatchVersion))
        {
            var etag = ifNoneMatchVersion.Trim();
            if (!etag.StartsWith('"'))
                etag = $"\"{etag}\"";
            message.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified)
                return EventLogPackageCatalogPullResult.Unchanged();

            if (!response.IsSuccessStatusCode)
                return EventLogPackageCatalogPullResult.Failed();

            var body = await response.Content.ReadFromJsonAsync<EventLogPackageCatalogResponse>(JsonOptions, cancellationToken);
            return body?.Packages is { Count: > 0 }
                ? EventLogPackageCatalogPullResult.Ok(body)
                : EventLogPackageCatalogPullResult.Failed();
        }
        catch
        {
            return EventLogPackageCatalogPullResult.Failed();
        }
    }

    private static string NormalizeBase(string? url) => (url ?? string.Empty).Trim().TrimEnd('/');
}
