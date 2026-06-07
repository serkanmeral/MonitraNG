using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Notifications;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Clients;

public class MngHubNotificationClient : IMngHubNotificationClient
{
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly MngHubNotificationSettings _settings;
    private readonly ILogger<MngHubNotificationClient> _logger;

    public MngHubNotificationClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngHubNotificationClient> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value.MngHub;
        _httpClient = httpClientFactory.CreateClient("MngHub");

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            var version = string.IsNullOrWhiteSpace(_settings.ApiVersion) ? "v1" : _settings.ApiVersion.Trim();
            _httpClient.BaseAddress = new Uri($"{baseUrl}/api/{version}/");
        }
    }

    public async Task PushUserNotificationAsync(
        UserNotificationPushRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("MngHub push disabled; skipping notification for user {UserId}", request.UserId);
            return;
        }

        if (_httpClient.BaseAddress == null)
        {
            _logger.LogWarning("MngHub BaseUrl not configured; skipping push for user {UserId}", request.UserId);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
            return;

        var body = new
        {
            userId = request.UserId.Trim(),
            payload = new
            {
                notificationId = request.NotificationId,
                title = request.Title,
                message = request.Message,
                notificationType = request.NotificationType,
                deepLink = request.DeepLink,
                severity = request.Severity,
                createdAt = request.CreatedAt ?? DateTime.UtcNow
            }
        };

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "internal/user-notify")
            {
                Content = JsonContent.Create(body, options: JsonOptions)
            };

            if (!string.IsNullOrWhiteSpace(_settings.InternalNotifyApiKey))
                httpRequest.Headers.TryAddWithoutValidation(NotifyApiKeyHeaderName, _settings.InternalNotifyApiKey.Trim());

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "MngHub user-notify failed HTTP {Status} for user {UserId}: {Body}",
                    (int)response.StatusCode,
                    request.UserId,
                    responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngHub user-notify failed for user {UserId} (non-fatal)", request.UserId);
        }
    }
}
