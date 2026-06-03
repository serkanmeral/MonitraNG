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

public sealed class WorkflowSchedulerClient : IWorkflowSchedulerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<WorkflowSchedulerClient> _logger;

    public WorkflowSchedulerClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowSchedulerClient> logger)
    {
        _logger = logger;
        var baseUrl = settings.Value.Scheduler.BaseUrl?.Trim();
        _httpClient = httpClientFactory.CreateClient("MngScheduler");
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
    }

    public async Task<WorkflowSchedulerUserJobDto?> GetUserJobAsync(
        string jobId,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Get, $"user/jobs/{Uri.EscapeDataString(jobId)}", bearerToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, "get user job", cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkflowSchedulerUserJobDto>(JsonOptions, cancellationToken);
    }

    public async Task<WorkflowSchedulerUserJobDto> CreateUserJobAsync(
        WorkflowSchedulerUserJobDto job,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Post, "user/jobs", bearerToken);
        request.Content = JsonContent.Create(job, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "create user job", cancellationToken);

        return await response.Content.ReadFromJsonAsync<WorkflowSchedulerUserJobDto>(JsonOptions, cancellationToken)
            ?? job;
    }

    public async Task<WorkflowSchedulerUserJobDto> UpdateUserJobAsync(
        WorkflowSchedulerUserJobDto job,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Put, $"user/jobs/{Uri.EscapeDataString(job.JobId)}", bearerToken);
        request.Content = JsonContent.Create(job, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "update user job", cancellationToken);

        return await response.Content.ReadFromJsonAsync<WorkflowSchedulerUserJobDto>(JsonOptions, cancellationToken)
            ?? job;
    }

    public async Task DeleteUserJobAsync(string jobId, string bearerToken, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Delete, $"user/jobs/{Uri.EscapeDataString(jobId)}", bearerToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        await EnsureSuccessAsync(response, "delete user job", cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress == null)
            throw new InvalidOperationException("MngScheduler base URL is not configured.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string bearerToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("MngScheduler {Operation} failed HTTP {Status}: {Body}", operation, (int)response.StatusCode, body);
        throw new InvalidOperationException($"MngScheduler {operation} failed (HTTP {(int)response.StatusCode}).");
    }
}
