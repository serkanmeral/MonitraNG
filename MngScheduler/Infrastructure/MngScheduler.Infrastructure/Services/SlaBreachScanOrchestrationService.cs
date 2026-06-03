using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Services;

public sealed class SlaBreachScanOrchestrationService : ISlaBreachScanOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngKeeperAuthClient _keeperAuth;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SlaBreachScanOrchestrationSettings _settings;
    private readonly WorkItemScheduleOrchestrationSettings _serviceAccountSettings;
    private readonly ILogger<SlaBreachScanOrchestrationService> _logger;

    public SlaBreachScanOrchestrationService(
        IMngKeeperAuthClient keeperAuth,
        IHttpClientFactory httpClientFactory,
        IOptions<MngSchedulerSettings> settings,
        ILogger<SlaBreachScanOrchestrationService> logger)
    {
        _keeperAuth = keeperAuth;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value.SlaBreachScanOrchestration;
        _serviceAccountSettings = settings.Value.WorkItemScheduleOrchestration;
        _logger = logger;
    }

    public async Task<SlaBreachScanOrchestrationResult> ScanWorkspaceAsync(
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return Fail(503, "SLA breach scan orchestration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Fail(400, "workspaceId is required.");
        }

        var tokenResult = await _keeperAuth.GetAccessTokenAsync(cancellationToken);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            _logger.LogWarning(
                "[SlaBreachScan] Keeper token failed workspaceId={WorkspaceId} status={Status} error={Error}",
                workspaceId,
                tokenResult.HttpStatusCode,
                tokenResult.ErrorMessage);

            return Fail(
                tokenResult.HttpStatusCode > 0 ? tokenResult.HttpStatusCode : 502,
                tokenResult.ErrorMessage ?? "Keeper token failed.");
        }

        var scanUrl = ResolveScanUrl(workspaceId);
        var client = _httpClientFactory.CreateClient("WorkItemScheduleExecute");

        using var request = new HttpRequestMessage(HttpMethod.Post, scanUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        var domainName = _serviceAccountSettings.ServiceAccount.DomainName;
        if (!string.IsNullOrWhiteSpace(domainName))
            request.Headers.TryAddWithoutValidation("X-Domain-Name", domainName);

        _logger.LogInformation(
            "[SlaBreachScan] POST scan workspaceId={WorkspaceId} url={Url}",
            workspaceId,
            scanUrl);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[SlaBreachScan] Scan failed workspaceId={WorkspaceId} HTTP {Status} body={Body}",
                workspaceId,
                (int)response.StatusCode,
                Truncate(body));

            return new SlaBreachScanOrchestrationResult
            {
                IsSuccess = false,
                HttpStatusCode = (int)response.StatusCode,
                ResponseBody = Truncate(body),
                ErrorMessage = $"MO scan-breaches HTTP {(int)response.StatusCode}"
            };
        }

        var responseCount = 0;
        var resolveCount = 0;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("responseBreachesProcessed", out var r))
                responseCount = r.GetInt32();
            if (doc.RootElement.TryGetProperty("resolveBreachesProcessed", out var s))
                resolveCount = s.GetInt32();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "[SlaBreachScan] Response JSON parse skipped");
        }

        _logger.LogInformation(
            "[SlaBreachScan] Scan success workspaceId={WorkspaceId} response={Response} resolve={Resolve}",
            workspaceId,
            responseCount,
            resolveCount);

        return new SlaBreachScanOrchestrationResult
        {
            IsSuccess = true,
            HttpStatusCode = (int)response.StatusCode,
            ResponseBody = Truncate(body),
            ResponseBreachesProcessed = responseCount,
            ResolveBreachesProcessed = resolveCount
        };
    }

    private string ResolveScanUrl(string workspaceId)
    {
        var template = _settings.ScanEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template))
            template = "http://mngoperations:5086/api/v1/sla/scan-breaches?workspaceId={workspaceId}";

        if (!template.Contains("{workspaceId}", StringComparison.Ordinal))
            throw new InvalidOperationException("SlaBreachScan ScanEndpointTemplate must contain {workspaceId}.");

        return template.Replace("{workspaceId}", Uri.EscapeDataString(workspaceId.Trim()), StringComparison.Ordinal);
    }

    private static SlaBreachScanOrchestrationResult Fail(int status, string message) =>
        new() { IsSuccess = false, HttpStatusCode = status, ErrorMessage = message };

    private static string Truncate(string? body, int max = 10240)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= max)
            return body ?? string.Empty;
        return body[..max] + "... [truncated]";
    }
}
