using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Infrastructure.Services;

/// <summary>
/// MngDataGateway API client implementasyonu.
/// </summary>
public class DataGatewayClient : IDataGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly MngWorkflowSettings _settings;
    private readonly ILogger<DataGatewayClient> _logger;

    public DataGatewayClient(
        HttpClient httpClient,
        IOptions<MngWorkflowSettings> settings,
        ILogger<DataGatewayClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<Dictionary<string, object>>> GetDataAsync(
        string datasetName,
        string? filter,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.DataGatewayBaseUrl.TrimEnd('/');
        var encName = Uri.EscapeDataString(datasetName);
        var url = $"{baseUrl}/api/v1/data/{encName}";
        if (!string.IsNullOrEmpty(filter))
            url += $"?filter={Uri.EscapeDataString(filter)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(authorizationHeader))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(content);
        return result ?? new List<Dictionary<string, object>>();
    }

    public async Task<Dictionary<string, object>?> GetByIdAsync(
        string datasetName,
        string dataId,
        string domainName,
        string? authorizationHeader,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _settings.DataGatewayBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/data/{Uri.EscapeDataString(datasetName)}/{Uri.EscapeDataString(dataId)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(authorizationHeader))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(content);
    }
}
