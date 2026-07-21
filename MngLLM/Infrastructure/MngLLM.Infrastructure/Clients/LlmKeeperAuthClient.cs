using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLLM.Application.Configuration;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;

namespace MngLLM.Infrastructure.Clients;

/// <summary>
/// Keeper password-grant client with in-memory token cache (singleton).
/// Used by DI AI to call MngDocument / DataGateway.
/// </summary>
public sealed class LlmKeeperAuthClient : ILlmKeeperAuthClient
{
    private const int RefreshBufferSeconds = 60;
    private const int DefaultExpiresInSeconds = 300;
    private const int MinCacheLifetimeSeconds = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngLLMSettings _settings;
    private readonly ILogger<LlmKeeperAuthClient> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedAccessToken;
    private DateTime _cachedExpiresAtUtc = DateTime.MinValue;

    public LlmKeeperAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MngLLMSettings> settings,
        ILogger<LlmKeeperAuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetCached(out var cached))
            return cached;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCached(out cached))
                return cached;

            var (token, expiresIn) = await RequestTokenAsync(cancellationToken);
            var lifetime = Math.Max(MinCacheLifetimeSeconds, expiresIn - RefreshBufferSeconds);
            _cachedAccessToken = token;
            _cachedExpiresAtUtc = DateTime.UtcNow.AddSeconds(lifetime);
            _logger.LogInformation(
                "MngLLM service token acquired for {Domain}\\{User}, cacheSeconds={Seconds}",
                _settings.ServiceAccount.DomainName,
                _settings.ServiceAccount.Username,
                lifetime);
            return token;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool TryGetCached(out string token)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _cachedExpiresAtUtc)
        {
            token = _cachedAccessToken!;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var account = _settings.ServiceAccount;
        if (string.IsNullOrWhiteSpace(account.DomainName)
            || string.IsNullOrWhiteSpace(account.Username)
            || string.IsNullOrWhiteSpace(account.Password))
        {
            throw new DiExtractException(
                "ServiceAccount DomainName, Username and Password must be configured in MngLLMSettings.",
                503);
        }

        var keeperBase = _settings.Actors.MngKeeper?.Trim();
        if (string.IsNullOrWhiteSpace(keeperBase))
            throw new DiExtractException("MngKeeper actor URL is not configured.", 503);

        var tokenUri = $"{keeperBase.TrimEnd('/')}/api/auth/token";
        var client = _httpClientFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            tokenUri,
            new KeeperTokenRequest
            {
                Username = account.Username,
                Password = account.Password,
                Domain = account.DomainName
            },
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Keeper token failed HTTP {Status}: {Body}",
                (int)response.StatusCode,
                body.Length > 300 ? body[..300] : body);
            throw new DiExtractException(
                $"Keeper token request failed ({(int)response.StatusCode}).",
                503);
        }

        var parsed = JsonSerializer.Deserialize<KeeperTokenResponse>(body, JsonOptions);
        var token = parsed?.ResolvedAccessToken;
        if (string.IsNullOrWhiteSpace(token))
            throw new DiExtractException("Keeper token response did not include accessToken.", 503);

        var expiresIn = parsed.ResolvedExpiresIn > 0 ? parsed.ResolvedExpiresIn : DefaultExpiresInSeconds;
        return (token, expiresIn);
    }

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
        public int ExpiresIn { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresInSnake { get; set; }

        public string? ResolvedAccessToken =>
            !string.IsNullOrWhiteSpace(AccessToken) ? AccessToken : AccessTokenSnake;

        public int ResolvedExpiresIn =>
            ExpiresIn > 0 ? ExpiresIn : ExpiresInSnake;
    }
}
