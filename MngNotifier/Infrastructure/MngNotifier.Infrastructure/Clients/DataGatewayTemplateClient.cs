using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Clients;

public sealed class DataGatewayTemplateClient : IDataGatewayTemplateClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<DataGatewayTemplateClient> _logger;

    public DataGatewayTemplateClient(
        IHttpClientFactory httpClientFactory,
        ILogger<DataGatewayTemplateClient> logger,
        IOptions<MngNotifierSettings> settings)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("MngDataGateway");

        var dg = settings.Value.DataGateway;
        var baseUrl = (dg.BaseUrl ?? "http://localhost:5010").TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(dg.ApiVersion) ? "v1" : dg.ApiVersion.Trim();
        _httpClient.BaseAddress = new Uri($"{baseUrl}/api/{version}/");
    }

    public Task<MailTemplateRecord?> GetTemplateByKeyAsync(string templateKey, string bearerToken, CancellationToken cancellationToken = default)
        => QuerySingleAsync<MailTemplateRecord>("@mail_templates", $"templateKey:eq:{templateKey}", bearerToken, cancellationToken);

    public Task<MailLayoutRecord?> GetLayoutByKeyAsync(string layoutKey, string bearerToken, CancellationToken cancellationToken = default)
        => QuerySingleAsync<MailLayoutRecord>("@mail_layouts", $"layoutKey:eq:{layoutKey}", bearerToken, cancellationToken);

    public Task<MailLayoutRecord?> GetDefaultLayoutAsync(string bearerToken, CancellationToken cancellationToken = default)
        => QuerySingleAsync<MailLayoutRecord>("@mail_layouts", "isDefault:eq:true", bearerToken, cancellationToken);

    private async Task<T?> QuerySingleAsync<T>(
        string dataset,
        string filter,
        string bearerToken,
        CancellationToken cancellationToken)
        where T : class
    {
        if (_httpClient.BaseAddress == null)
        {
            _logger.LogWarning("DataGateway BaseUrl not configured");
            return null;
        }

        var url = $"data/{Uri.EscapeDataString(dataset)}?filter={Uri.EscapeDataString(filter)}&limit=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("DG query failed {Dataset} filter={Filter} HTTP {Status}: {Body}",
                dataset, filter, (int)response.StatusCode, body);
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() == 0)
            return null;

        return JsonSerializer.Deserialize<T>(json[0].GetRawText(), JsonOptions);
    }
}
