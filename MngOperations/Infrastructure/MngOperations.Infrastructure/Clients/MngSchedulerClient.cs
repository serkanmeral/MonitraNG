using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Schedules;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Clients;

public sealed class MngSchedulerClient : IMngSchedulerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MngSchedulerClient> _logger;

    public MngSchedulerClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngSchedulerClient> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _logger = logger;
        var baseUrl = settings.Value.Actors.MngScheduler?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient = httpClientFactory.CreateClient("MngScheduler");
            return;
        }

        _httpClient = httpClientFactory.CreateClient("MngScheduler");
        _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/v1/");
    }

    public async Task<SchedulerUserJobDto?> GetUserJobAsync(
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
        return await response.Content.ReadFromJsonAsync<SchedulerUserJobDto>(JsonOptions, cancellationToken);
    }

    public async Task<SchedulerUserJobDto> CreateUserJobAsync(
        SchedulerUserJobDto job,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(HttpMethod.Post, "user/jobs", bearerToken);
        request.Content = JsonContent.Create(job, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "create user job", cancellationToken);

        var created = await response.Content.ReadFromJsonAsync<SchedulerUserJobDto>(JsonOptions, cancellationToken);
        return created ?? job;
    }

    public async Task<SchedulerUserJobDto> UpdateUserJobAsync(
        SchedulerUserJobDto job,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var path = $"user/jobs/{Uri.EscapeDataString(job.JobId)}";
        using var request = CreateRequest(HttpMethod.Put, path, bearerToken);
        request.Content = JsonContent.Create(job, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "update user job", cancellationToken);

        var updated = await response.Content.ReadFromJsonAsync<SchedulerUserJobDto>(JsonOptions, cancellationToken);
        return updated ?? job;
    }

    public async Task DeleteUserJobAsync(
        string jobId,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var request = CreateRequest(
            HttpMethod.Delete,
            $"user/jobs/{Uri.EscapeDataString(jobId)}",
            bearerToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return;

        await EnsureSuccessAsync(response, "delete user job", cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (_httpClient.BaseAddress == null)
        {
            throw new OperationCoreException(
                "SCHEDULER_NOT_CONFIGURED",
                "MngScheduler base URL is not configured.",
                "MngScheduler adresi yapılandırılmamış.",
                503);
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string bearerToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "MngScheduler {Operation} failed HTTP {Status}: {Body}",
            operation,
            (int)response.StatusCode,
            body);

        throw new OperationCoreException(
            "SCHEDULER_SYNC_FAILED",
            $"MngScheduler {operation} failed (HTTP {(int)response.StatusCode}).",
            $"MngScheduler {operation} başarısız (HTTP {(int)response.StatusCode}).",
            (int)response.StatusCode >= 500 ? 502 : (int)response.StatusCode);
    }
}
