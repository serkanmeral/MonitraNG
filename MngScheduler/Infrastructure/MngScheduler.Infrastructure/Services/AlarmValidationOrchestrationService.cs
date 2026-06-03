using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Services;

public sealed class AlarmValidationOrchestrationService : IAlarmValidationOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngKeeperAuthClient _keeperAuth;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AlarmValidationOrchestrationSettings _settings;
    private readonly WorkItemScheduleOrchestrationSettings _serviceAccountSettings;
    private readonly ILogger<AlarmValidationOrchestrationService> _logger;

    public AlarmValidationOrchestrationService(
        IMngKeeperAuthClient keeperAuth,
        IHttpClientFactory httpClientFactory,
        IOptions<MngSchedulerSettings> settings,
        ILogger<AlarmValidationOrchestrationService> logger)
    {
        _keeperAuth = keeperAuth;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value.AlarmValidationOrchestration;
        _serviceAccountSettings = settings.Value.WorkItemScheduleOrchestration;
        _logger = logger;
    }

    public async Task<AlarmValidationOrchestrationResult> RunValidationAsync(
        string domainName,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return Fail(503, "Alarm validation orchestration is disabled.");

        if (string.IsNullOrWhiteSpace(domainName))
            return Fail(400, "domainName is required.");

        var tokenResult = await _keeperAuth.GetAccessTokenAsync(cancellationToken);
        if (!tokenResult.Success || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            _logger.LogWarning(
                "[AlarmValidation] Keeper token failed domain={Domain} status={Status} error={Error}",
                domainName,
                tokenResult.HttpStatusCode,
                tokenResult.ErrorMessage);

            return Fail(
                tokenResult.HttpStatusCode > 0 ? tokenResult.HttpStatusCode : 502,
                tokenResult.ErrorMessage ?? "Keeper token failed.");
        }

        var validationUrl = ResolveValidationUrl(domainName);
        var client = _httpClientFactory.CreateClient("WorkItemScheduleExecute");

        using var request = new HttpRequestMessage(HttpMethod.Post, validationUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
        request.Headers.TryAddWithoutValidation("X-Domain-Name", domainName.Trim());

        _logger.LogInformation(
            "[AlarmValidation] POST validation domain={Domain} url={Url}",
            domainName,
            validationUrl);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[AlarmValidation] Validation failed domain={Domain} HTTP {Status} body={Body}",
                domainName,
                (int)response.StatusCode,
                Truncate(body));

            return new AlarmValidationOrchestrationResult
            {
                IsSuccess = false,
                HttpStatusCode = (int)response.StatusCode,
                ResponseBody = Truncate(body),
                ErrorMessage = $"MA validation/run HTTP {(int)response.StatusCode}"
            };
        }

        var correlationResolved = 0;
        var scheduledRaised = 0;
        var scheduledResolved = 0;
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("correlationResolved", out var cr))
                correlationResolved = cr.GetInt32();
            if (doc.RootElement.TryGetProperty("scheduledRaised", out var sr))
                scheduledRaised = sr.GetInt32();
            if (doc.RootElement.TryGetProperty("scheduledResolved", out var sres))
                scheduledResolved = sres.GetInt32();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "[AlarmValidation] Response JSON parse skipped");
        }

        _logger.LogInformation(
            "[AlarmValidation] Success domain={Domain} correlationResolved={CorrelationResolved} scheduledRaised={ScheduledRaised}",
            domainName,
            correlationResolved,
            scheduledRaised);

        return new AlarmValidationOrchestrationResult
        {
            IsSuccess = true,
            HttpStatusCode = (int)response.StatusCode,
            ResponseBody = Truncate(body),
            CorrelationResolved = correlationResolved,
            ScheduledRaised = scheduledRaised,
            ScheduledResolved = scheduledResolved
        };
    }

    private string ResolveValidationUrl(string domainName)
    {
        var template = _settings.ValidationEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template))
            template = "http://mngalarm:5087/api/v1/validation/run";

        if (template.Contains("{domainName}", StringComparison.Ordinal))
        {
            return template.Replace("{domainName}", Uri.EscapeDataString(domainName.Trim()), StringComparison.Ordinal);
        }

        return template;
    }

    private static AlarmValidationOrchestrationResult Fail(int status, string message) =>
        new() { IsSuccess = false, HttpStatusCode = status, ErrorMessage = message };

    private static string Truncate(string? body, int max = 10240)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= max)
            return body ?? string.Empty;
        return body[..max] + "... [truncated]";
    }
}
