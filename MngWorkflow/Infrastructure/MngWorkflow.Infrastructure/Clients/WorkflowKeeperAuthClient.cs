using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Infrastructure.Clients;

public sealed class WorkflowKeeperAuthClient : IWorkflowKeeperAuthClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SchedulerSettings _scheduler;
    private readonly ILogger<WorkflowKeeperAuthClient> _logger;

    public WorkflowKeeperAuthClient(
        IHttpClientFactory httpClientFactory,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowKeeperAuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scheduler = settings.Value.Scheduler;
        _logger = logger;
    }

    public async Task<string?> GetServiceAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var account = _scheduler.ServiceAccount;
        if (string.IsNullOrWhiteSpace(account.DomainName)
            || string.IsNullOrWhiteSpace(account.Username)
            || string.IsNullOrWhiteSpace(account.Password))
        {
            _logger.LogDebug("Workflow scheduler service account is not configured.");
            return null;
        }

        var keeperBase = _scheduler.MngKeeperBaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(keeperBase))
        {
            _logger.LogWarning("MngKeeper base URL is not configured for workflow scheduler sync.");
            return null;
        }

        var tokenUri = $"{keeperBase.TrimEnd('/')}/api/auth/token";
        var client = _httpClientFactory.CreateClient("MngKeeperAuth");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = JsonContent.Create(new KeeperTokenRequest
            {
                Username = account.Username,
                Password = account.Password,
                Domain = account.DomainName
            })
        };

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Keeper token request failed HTTP {Status}", (int)response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<KeeperTokenResponse>(body, JsonOptions);
        return tokenResponse?.ResolvedAccessToken;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
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

        public string? ResolvedAccessToken =>
            !string.IsNullOrWhiteSpace(AccessToken) ? AccessToken : AccessTokenSnake;
    }
}
