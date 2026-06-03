using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Workflow;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Clients;

public sealed class MngWorkflowClient : IMngWorkflowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MngWorkflowClient> _logger;

    public MngWorkflowClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngWorkflowClient> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("MngWorkflow");
        var baseUrl = settings.Value.Workflow.BaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
    }

    public async Task<StartWorkflowRunResponse> StartRunAsync(
        string domainName,
        string bearerToken,
        StartWorkflowRunRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "runs");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (!string.IsNullOrWhiteSpace(domainName))
            httpRequest.Headers.TryAddWithoutValidation("X-Domain-Name", domainName.Trim());
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "MngWorkflow start run failed HTTP {Status}: {Body}",
                (int)response.StatusCode,
                body.Length > 500 ? body[..500] : body);

            throw new OperationCoreException(
                "WORKFLOW_START_FAILED",
                $"MngWorkflow start run failed (HTTP {(int)response.StatusCode}).",
                $"Workflow başlatılamadı (HTTP {(int)response.StatusCode}).",
                (int)response.StatusCode >= 500 ? 502 : (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<StartWorkflowRunResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty workflow start response.");
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress == null)
        {
            throw new OperationCoreException(
                "WORKFLOW_NOT_CONFIGURED",
                "MngWorkflow base URL is not configured.",
                "MngWorkflow adresi yapılandırılmamış.",
                503);
        }
    }
}
