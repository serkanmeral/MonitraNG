using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Infrastructure.Clients;

public sealed class WorkflowOperationsClient : IWorkflowOperationsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkflowOperationsClient> _logger;

    public WorkflowOperationsClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowOperationsClient> logger)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("MngOperations");
        var baseUrl = settings.Value.Operations.BaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
    }

    public async Task<WorkflowCreateWorkItemResponse> CreateFromOriginAsync(
        string bearerToken,
        WorkflowCreateFromOriginRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "work-items/from-origin");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "create from-origin", cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<WorkflowCreateWorkItemResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty create from-origin response.");

        return body;
    }

    public async Task<WorkflowTransitionWorkItemResponse> ApplyTransitionAsync(
        string bearerToken,
        string workItemId,
        string transitionKey,
        WorkflowTransitionWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var path = $"work-items/{Uri.EscapeDataString(workItemId)}/transitions/{Uri.EscapeDataString(transitionKey)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "apply transition", cancellationToken);

        return await response.Content.ReadFromJsonAsync<WorkflowTransitionWorkItemResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty transition response.");
    }

    public async Task<WorkflowWorkItemDto> PatchWorkItemAsync(
        string bearerToken,
        string workItemId,
        WorkflowPatchWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var path = $"work-items/{Uri.EscapeDataString(workItemId)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, path);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        await EnsureSuccessAsync(response, "patch work item", cancellationToken);

        return await response.Content.ReadFromJsonAsync<WorkflowWorkItemDto>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Empty patch work item response.");
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress == null)
            throw new InvalidOperationException("MngOperations base URL is not configured.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "MngOperations {Operation} failed HTTP {Status}: {Body}",
            operation,
            (int)response.StatusCode,
            body.Length > 500 ? body[..500] : body);

        throw new WorkflowOperationsException(
            $"MngOperations {operation} failed (HTTP {(int)response.StatusCode}).",
            (int)response.StatusCode);
    }
}

public sealed class WorkflowOperationsException : Exception
{
    public int StatusCode { get; }

    public WorkflowOperationsException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public bool IsRetryable => StatusCode >= 500 || StatusCode == (int)HttpStatusCode.RequestTimeout;
}
