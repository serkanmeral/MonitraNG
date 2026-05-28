using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Services;

public sealed class WorkItemScheduleOrchestrationService : IWorkItemScheduleOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngKeeperAuthClient _keeperAuth;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WorkItemScheduleOrchestrationSettings _settings;
    private readonly ILogger<WorkItemScheduleOrchestrationService> _logger;

    public WorkItemScheduleOrchestrationService(
        IMngKeeperAuthClient keeperAuth,
        IHttpClientFactory httpClientFactory,
        IOptions<MngSchedulerSettings> settings,
        ILogger<WorkItemScheduleOrchestrationService> logger)
    {
        _keeperAuth = keeperAuth;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value.WorkItemScheduleOrchestration;
        _logger = logger;
    }

    public async Task<WorkItemScheduleOrchestrationResult> ExecuteScheduleAsync(
        string scheduleDataId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            return Fail(503, "WorkItemSchedule orchestration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(scheduleDataId))
        {
            return Fail(400, "scheduleDataId is required.");
        }

        var tokenResult = await _keeperAuth.GetAccessTokenAsync(cancellationToken);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            _logger.LogWarning(
                "[WorkItemSchedule] Keeper token failed scheduleId={ScheduleId} status={Status} error={Error}",
                scheduleDataId,
                tokenResult.HttpStatusCode,
                tokenResult.ErrorMessage);

            return Fail(
                tokenResult.HttpStatusCode > 0 ? tokenResult.HttpStatusCode : 502,
                tokenResult.ErrorMessage ?? "Keeper token failed.");
        }

        var executeUrl = ResolveExecuteUrl(scheduleDataId);
        var client = _httpClientFactory.CreateClient("WorkItemScheduleExecute");

        using var request = new HttpRequestMessage(HttpMethod.Post, executeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

        _logger.LogInformation(
            "[WorkItemSchedule] POST execute scheduleId={ScheduleId} url={Url}",
            scheduleDataId,
            executeUrl);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[WorkItemSchedule] Execute failed scheduleId={ScheduleId} HTTP {Status} body={Body}",
                scheduleDataId,
                (int)response.StatusCode,
                Truncate(body));

            return new WorkItemScheduleOrchestrationResult
            {
                IsSuccess = false,
                HttpStatusCode = (int)response.StatusCode,
                ResponseBody = Truncate(body),
                ErrorMessage = $"MO execute HTTP {(int)response.StatusCode}"
            };
        }

        string? workItemId = null;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("workItemId", out var idProp))
                workItemId = idProp.GetString();
            else if (doc.RootElement.TryGetProperty("workItem", out var wi)
                     && wi.TryGetProperty("id", out var nestedId))
                workItemId = nestedId.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "[WorkItemSchedule] Execute response JSON parse skipped");
        }

        _logger.LogInformation(
            "[WorkItemSchedule] Execute success scheduleId={ScheduleId} workItemId={WorkItemId}",
            scheduleDataId,
            workItemId);

        return new WorkItemScheduleOrchestrationResult
        {
            IsSuccess = true,
            HttpStatusCode = (int)response.StatusCode,
            ResponseBody = Truncate(body),
            WorkItemId = workItemId
        };
    }

    private string ResolveExecuteUrl(string scheduleDataId)
    {
        var template = _settings.ExecuteEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template))
        {
            var moBase = _settings.GatewayOperationsFromOrigin;
            if (!string.IsNullOrWhiteSpace(moBase)
                && moBase.Contains("/work-items/from-origin", StringComparison.OrdinalIgnoreCase))
            {
                var root = moBase[..moBase.IndexOf("/work-items", StringComparison.OrdinalIgnoreCase)];
                template = $"{root}/work-item-schedules/{{scheduleId}}/execute";
            }
            else
            {
                template = "http://mngoperations:5086/api/v1/work-item-schedules/{scheduleId}/execute";
            }
        }

        if (!template.Contains("{scheduleId}", StringComparison.Ordinal))
            throw new InvalidOperationException("WorkItemSchedule ExecuteEndpointTemplate must contain {scheduleId}.");

        return template.Replace("{scheduleId}", scheduleDataId, StringComparison.Ordinal);
    }

    private static WorkItemScheduleOrchestrationResult Fail(int status, string message) =>
        new() { IsSuccess = false, HttpStatusCode = status, ErrorMessage = message };

    private static string Truncate(string? body, int max = 10240)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= max)
            return body ?? string.Empty;
        return body[..max] + "... [truncated]";
    }
}
