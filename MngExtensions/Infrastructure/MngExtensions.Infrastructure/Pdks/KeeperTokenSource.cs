using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngExtensions.Application.Configuration;
using MngExtensions.Application.Pdks;

namespace MngExtensions.Infrastructure.Pdks;

public sealed class KeeperTokenSource : IKeeperTokenSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MngExtensionsSettings> _settings;
    private readonly ILogger<KeeperTokenSource> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _cachedToken;
    private DateTime _cachedExpiresAtUtc = DateTime.MinValue;

    public KeeperTokenSource(
        IHttpClientFactory httpClientFactory,
        IOptions<MngExtensionsSettings> settings,
        ILogger<KeeperTokenSource> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var account = _settings.Value.ServiceAccount;
        if (string.IsNullOrWhiteSpace(account.DomainName)
            || string.IsNullOrWhiteSpace(account.Username)
            || string.IsNullOrWhiteSpace(account.Password))
        {
            throw new InvalidOperationException("PDKS service account is not configured.");
        }

        if (!string.IsNullOrWhiteSpace(_cachedToken) && _cachedExpiresAtUtc > DateTime.UtcNow.AddSeconds(30))
            return _cachedToken;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken) && _cachedExpiresAtUtc > DateTime.UtcNow.AddSeconds(30))
                return _cachedToken;

            var keeper = _settings.Value.Keeper;
            var path = string.IsNullOrWhiteSpace(keeper.TokenPath) ? "/api/auth/token" : keeper.TokenPath.Trim();
            if (!path.StartsWith('/'))
                path = "/" + path;

            var client = _httpClientFactory.CreateClient("MngKeeper");
            using var response = await client.PostAsJsonAsync(
                $"{keeper.BaseUrl.TrimEnd('/')}{path}",
                new KeeperTokenRequest(account.Username, account.Password, account.DomainName),
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Keeper token request failed with status {Status}", (int)response.StatusCode);
                throw new InvalidOperationException("Keeper token request failed.");
            }

            var parsed = JsonSerializer.Deserialize<KeeperTokenResponse>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(parsed?.AccessToken))
                throw new InvalidOperationException("Keeper token response has no access token.");

            var expiresIn = parsed.ExpiresIn is > 0 ? parsed.ExpiresIn.Value : 300;
            _cachedToken = parsed.AccessToken;
            _cachedExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(30, expiresIn - 60));
            return _cachedToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private sealed record KeeperTokenRequest(string Username, string Password, string Domain);

    private sealed class KeeperTokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expiresIn")]
        public int? ExpiresIn { get; set; }
    }
}
