using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Clients;

/// <summary>
/// MngKeeper kullanıcı dizini istemcisi (GET api/User/{id}). Keeper'da toplu endpoint yok;
/// tekil çözüm yapılır, cache <see cref="MngOperations.Application.Interfaces.IPersonDirectory"/>'de.
/// </summary>
public sealed class MngKeeperClient : IKeeperDirectoryClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MngKeeperClient> _logger;

    public MngKeeperClient(
        IHttpClientFactory httpClientFactory,
        ILogger<MngKeeperClient> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("MngKeeper");

        var baseUrl = settings.Value.Actors.MngKeeper?.Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl))
            _httpClient.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/api/");
    }

    public async Task<PersonDisplayDto?> GetUserAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null || string.IsNullOrWhiteSpace(userId))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"User/{Uri.EscapeDataString(userId)}");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MngKeeper GetUser failed for {UserId}: HTTP {Status}",
                    userId, (int)response.StatusCode);
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<KeeperUserEnvelope>(JsonOptions, cancellationToken);
            var user = envelope?.User;
            if (user == null)
                return null;

            return new PersonDisplayDto
            {
                Id = userId,
                Name = BuildName(user),
                Title = string.IsNullOrWhiteSpace(user.Title) ? null : user.Title,
                IsActive = user.IsActive
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngKeeper GetUser error for {UserId}", userId);
            return null;
        }
    }

    private static string BuildName(KeeperUserDto user)
    {
        var full = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrWhiteSpace(full))
            return full;
        if (!string.IsNullOrWhiteSpace(user.Username))
            return user.Username!;
        return string.IsNullOrWhiteSpace(user.Email) ? string.Empty : user.Email!;
    }

    private sealed class KeeperUserEnvelope
    {
        [JsonPropertyName("user")]
        public KeeperUserDto? User { get; set; }

        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }
    }

    private sealed class KeeperUserDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Title { get; set; }
        public bool? IsActive { get; set; }
    }
}
