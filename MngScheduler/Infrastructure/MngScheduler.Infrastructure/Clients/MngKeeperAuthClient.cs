using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Clients;

public class MngKeeperAuthClient : IMngKeeperAuthClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MngKeeperAuthClient> _logger;
    private readonly MngSchedulerSettings _settings;

    public MngKeeperAuthClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngKeeperAuthClient> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<MngKeeperAccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var oc = _settings.WorkItemScheduleOrchestration;
        var account = oc.ServiceAccount;

        if (string.IsNullOrWhiteSpace(account.DomainName)
            || string.IsNullOrWhiteSpace(account.Username)
            || string.IsNullOrWhiteSpace(account.Password))
        {
            return Fail(0, "WorkItemSchedule ServiceAccount DomainName, Username and Password are required.");
        }

        var keeperBase = ResolveKeeperBaseUrl();
        var tokenPath = string.IsNullOrWhiteSpace(oc.KeeperTokenPath)
            ? "/api/auth/token"
            : oc.KeeperTokenPath.Trim();
        if (!tokenPath.StartsWith('/'))
            tokenPath = "/" + tokenPath;

        var tokenUri = $"{keeperBase.TrimEnd('/')}{tokenPath}";

        var client = _httpClientFactory.CreateClient("MngKeeperAuth");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = JsonContent.Create(new KeeperTokenRequest
            {
                Username = account.Username,
                Password = account.Password,
                Domain = account.DomainName,
            }),
        };

        _logger.LogDebug(
            "[WorkItemSchedule] Requesting Keeper token domain={Domain} user={Username} url={Url}",
            account.DomainName,
            account.Username,
            tokenUri);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WorkItemSchedule] Keeper token request failed");
            return Fail(0, ex.Message);
        }

        var statusCode = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[WorkItemSchedule] Keeper token HTTP {Status} body={Body}",
                statusCode,
                Truncate(body, 300));
            return Fail(statusCode, $"Keeper token HTTP {statusCode}");
        }

        try
        {
            var tokenResponse = JsonSerializer.Deserialize<KeeperTokenResponse>(body, JsonOptions);
            var accessToken = tokenResponse?.ResolvedAccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogWarning("[WorkItemSchedule] Keeper response missing accessToken");
                return Fail(statusCode, "Keeper response missing accessToken");
            }

            _logger.LogInformation(
                "[WorkItemSchedule] Keeper token acquired domain={Domain} user={Username}",
                account.DomainName,
                account.Username);

            return new MngKeeperAccessTokenResult
            {
                Success = true,
                AccessToken = accessToken,
                ExpiresInSeconds = tokenResponse?.ResolvedExpiresIn,
                HttpStatusCode = statusCode,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "[WorkItemSchedule] Invalid Keeper token JSON");
            return Fail(statusCode, "Invalid Keeper token response");
        }
    }

    private string ResolveKeeperBaseUrl()
    {
        var configured = _settings.WorkItemScheduleOrchestration.MngKeeperBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        var actor = _settings.Actors.MngKeeper;
        if (!string.IsNullOrWhiteSpace(actor))
            return actor.Trim();

        throw new InvalidOperationException(
            "MngKeeper base URL is not configured. Set WorkItemScheduleOrchestration:MngKeeperBaseUrl or Actors:MngKeeper.");
    }

    private static MngKeeperAccessTokenResult Fail(int httpStatusCode, string message) =>
        new()
        {
            Success = false,
            HttpStatusCode = httpStatusCode,
            ErrorMessage = message,
        };

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value ?? string.Empty : value[..max] + "…";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class KeeperTokenRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;
    }

    private sealed class KeeperTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessTokenSnake { get; set; }

        [JsonPropertyName("expiresIn")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresInSnake { get; set; }

        public string? ResolvedAccessToken =>
            !string.IsNullOrWhiteSpace(AccessToken) ? AccessToken : AccessTokenSnake;

        public int? ResolvedExpiresIn => ExpiresIn ?? ExpiresInSnake;
    }
}
