using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;

namespace MngAlarm.Infrastructure.Clients;

public interface IAlarmDispatchTokenProvider
{
    Task<string?> GetServiceTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class AlarmDispatchTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MngAlarmSettings> settings,
    ILogger<AlarmDispatchTokenProvider> logger) : IAlarmDispatchTokenProvider
{
    public async Task<string?> GetServiceTokenAsync(CancellationToken cancellationToken = default)
    {
        var dispatch = settings.Value.NotificationDispatch;
        if (!string.IsNullOrWhiteSpace(dispatch.StaticServiceToken))
            return dispatch.StaticServiceToken.Trim();

        var account = dispatch.ServiceAccount;
        if (string.IsNullOrWhiteSpace(account.DomainName)
            || string.IsNullOrWhiteSpace(account.Username)
            || string.IsNullOrWhiteSpace(account.Password))
        {
            logger.LogDebug("Alarm notification dispatch: service account not configured");
            return null;
        }

        var baseUrl = dispatch.MngKeeperBaseUrl.TrimEnd('/');
        var client = httpClientFactory.CreateClient("MngKeeper");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/auth/token")
        {
            Content = JsonContent.Create(new
            {
                username = account.Username,
                password = account.Password,
                domain = account.DomainName,
            }),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Keeper token for alarm dispatch failed HTTP {Status}", (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<KeeperTokenEnvelope>(JsonOptions, cancellationToken);
        return body?.ResolvedAccessToken;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class KeeperTokenEnvelope
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessTokenSnake { get; set; }

        public string? ResolvedAccessToken =>
            !string.IsNullOrWhiteSpace(AccessToken) ? AccessToken : AccessTokenSnake;
    }
}

public interface IAlarmOpNotificationsClient
{
    Task CreateAsync(
        string bearerToken,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmOpNotificationsClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MngAlarmSettings> settings,
    ILogger<AlarmOpNotificationsClient> logger) : IAlarmOpNotificationsClient
{
    private const string Dataset = "op_notifications";

    public async Task CreateAsync(
        string bearerToken,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        var dg = settings.Value.NotificationDispatch.DataGateway;
        var baseUrl = dg.BaseUrl.TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(dg.ApiVersion) ? "v1" : dg.ApiVersion.Trim();
        var client = httpClientFactory.CreateClient("MngDataGateway");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/{version}/data/{Dataset}")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("op_notifications create failed HTTP {Status}: {Body}", (int)response.StatusCode, body);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

public interface IAlarmHubNotificationClient
{
    Task PushAsync(
        string userId,
        string title,
        string message,
        string notificationType,
        string? deepLink,
        string? severity,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmHubNotificationClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MngAlarmSettings> settings,
    ILogger<AlarmHubNotificationClient> logger) : IAlarmHubNotificationClient
{
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    public async Task PushAsync(
        string userId,
        string title,
        string message,
        string notificationType,
        string? deepLink,
        string? severity,
        CancellationToken cancellationToken = default)
    {
        var hub = settings.Value.NotificationDispatch.MngHub;
        if (!hub.Enabled || string.IsNullOrWhiteSpace(userId))
            return;

        var baseUrl = hub.BaseUrl.TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(hub.ApiVersion) ? "v1" : hub.ApiVersion.Trim();
        var client = httpClientFactory.CreateClient("MngHub");
        var body = new
        {
            userId = userId.Trim(),
            payload = new
            {
                title,
                message,
                notificationType,
                deepLink,
                severity,
                createdAt = DateTime.UtcNow,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/{version}/internal/user-notify")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(hub.InternalNotifyApiKey))
            request.Headers.TryAddWithoutValidation(NotifyApiKeyHeaderName, hub.InternalNotifyApiKey.Trim());

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Hub user-notify failed HTTP {Status} for user {UserId}: {Body}",
                (int)response.StatusCode,
                userId,
                responseBody);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

public interface IAlarmNotifiersDispatchClient
{
    Task SendTemplateAsync(
        string bearerToken,
        IReadOnlyList<string> to,
        string templateKey,
        string? subjectOverride,
        Dictionary<string, object?> context,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmNotifiersDispatchClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MngAlarmSettings> settings,
    ILogger<AlarmNotifiersDispatchClient> logger) : IAlarmNotifiersDispatchClient
{
    public async Task SendTemplateAsync(
        string bearerToken,
        IReadOnlyList<string> to,
        string templateKey,
        string? subjectOverride,
        Dictionary<string, object?> context,
        CancellationToken cancellationToken = default)
    {
        var notifier = settings.Value.NotificationDispatch.MngNotifiers;
        if (!notifier.Enabled || to.Count == 0)
            return;

        var baseUrl = notifier.BaseUrl.TrimEnd('/');
        var version = string.IsNullOrWhiteSpace(notifier.ApiVersion) ? "v1" : notifier.ApiVersion.Trim();
        var client = httpClientFactory.CreateClient("MngNotifiers");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/{version}/notifications/send-template")
        {
            Content = JsonContent.Create(new
            {
                to,
                templateKey,
                subject = subjectOverride,
                context,
            }, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("send-template failed HTTP {Status}: {Body}", (int)response.StatusCode, body);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

public sealed record AlarmKeeperRecipient(string PersonId, string Email, string DisplayName);

public interface IAlarmKeeperUsersClient
{
    Task<IReadOnlyDictionary<string, string>> ResolveEmailsAsync(
        IReadOnlyList<string> personIds,
        string bearerToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlarmKeeperRecipient>> ResolveRecipientsAsync(
        IReadOnlyList<string> personIds,
        string bearerToken,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmKeeperUsersClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MngAlarmSettings> settings,
    ILogger<AlarmKeeperUsersClient> logger) : IAlarmKeeperUsersClient
{
    public async Task<IReadOnlyDictionary<string, string>> ResolveEmailsAsync(
        IReadOnlyList<string> personIds,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        var recipients = await ResolveRecipientsAsync(personIds, bearerToken, cancellationToken);
        return recipients
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .ToDictionary(r => r.PersonId, r => r.Email, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<AlarmKeeperRecipient>> ResolveRecipientsAsync(
        IReadOnlyList<string> personIds,
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        var result = new List<AlarmKeeperRecipient>();
        if (personIds.Count == 0 || string.IsNullOrWhiteSpace(bearerToken))
            return result;

        var baseUrl = settings.Value.NotificationDispatch.MngKeeperBaseUrl.TrimEnd('/');
        var client = httpClientFactory.CreateClient("MngKeeper");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/User/by-ids")
        {
            Content = JsonContent.Create(new { ids = personIds }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Keeper User/by-ids failed HTTP {Status}", (int)response.StatusCode);
            return result;
        }

        var envelope = await response.Content.ReadFromJsonAsync<KeeperUsersEnvelope>(JsonOptions, cancellationToken);
        if (envelope?.Users == null)
            return result;

        foreach (var user in envelope.Users)
        {
            var personId = user.ResolvedPersonId;
            if (string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(user.Email))
                continue;
            result.Add(new AlarmKeeperRecipient(
                personId,
                user.Email.Trim(),
                user.ResolvedDisplayName));
        }

        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class KeeperUsersEnvelope
    {
        public List<KeeperUserRow>? Users { get; set; }
    }

    private sealed class KeeperUserRow
    {
        public string? Id { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public string? ResolvedPersonId =>
            !string.IsNullOrWhiteSpace(UserId) ? UserId.Trim()
            : !string.IsNullOrWhiteSpace(Id) ? Id.Trim()
            : null;

        public string ResolvedDisplayName
        {
            get
            {
                var name = $"{FirstName} {LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
                if (!string.IsNullOrWhiteSpace(Username))
                    return Username.Trim();
                return ResolvedPersonId ?? "Kullanici";
            }
        }
    }
}
