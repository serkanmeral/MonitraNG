using System.Net.Http.Json;
using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Transport;

public interface ICollectorClient
{
    Task<IngestBatchResponse?> SendBatchAsync(IngestBatchRequest request, CancellationToken cancellationToken = default);
    Task<bool> HealthAsync(CancellationToken cancellationToken = default);
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

    private static string NormalizeBase(string? url) => (url ?? string.Empty).Trim().TrimEnd('/');
}
