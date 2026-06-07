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
/// MngKeeper kullanıcı/grup dizini istemcisi. Toplu çözüm POST api/User/by-ids & api/Group/by-ids ile
/// tek istekte yapılır (N+1'i önler); tekil GET api/User/{id} & GET api/Group/{id} yedek olarak kalır.
/// Cache <see cref="MngOperations.Application.Interfaces.IPersonDirectory"/>/<see cref="MngOperations.Application.Interfaces.IGroupDirectory"/>'de.
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
                Email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email.Trim(),
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

    public async Task<PersonDisplayDto?> GetGroupAsync(
        string groupId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null || string.IsNullOrWhiteSpace(groupId))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"Group/{Uri.EscapeDataString(groupId)}");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MngKeeper GetGroup failed for {GroupId}: HTTP {Status}",
                    groupId, (int)response.StatusCode);
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<KeeperGroupEnvelope>(JsonOptions, cancellationToken);
            var group = envelope?.Group;
            if (group == null)
                return null;

            return new PersonDisplayDto
            {
                Id = groupId,
                Name = string.IsNullOrWhiteSpace(group.Name) ? groupId : group.Name!,
                IsActive = group.IsActive
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngKeeper GetGroup error for {GroupId}", groupId);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetUsersAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, PersonDisplayDto>(StringComparer.Ordinal);
        var list = (ids ?? Enumerable.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (_httpClient.BaseAddress == null || list.Count == 0)
            return result;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "User/by-ids")
            {
                Content = JsonContent.Create(new { ids = list })
            };
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MngKeeper GetUsersByIds failed ({Count} id): HTTP {Status}", list.Count, (int)response.StatusCode);
                return result;
            }

            var envelope = await response.Content.ReadFromJsonAsync<KeeperUsersByIdsEnvelope>(JsonOptions, cancellationToken);
            var users = envelope?.Users;
            if (users == null || users.Count == 0)
                return result;

            // İstenen id (girişteki) → kullanıcı; bir kullanıcı hem __dataId hem keycloak sub ile eşleşebilir.
            var requested = new HashSet<string>(list, StringComparer.Ordinal);
            foreach (var u in users)
            {
                var display = new PersonDisplayDto
                {
                    Id = string.Empty,
                    Name = BuildName(u),
                    Email = string.IsNullOrWhiteSpace(u.Email) ? null : u.Email.Trim(),
                    Title = string.IsNullOrWhiteSpace(u.Title) ? null : u.Title,
                    IsActive = u.IsActive
                };

                foreach (var matchId in new[] { u.UserId, u.KeycloakUserId })
                {
                    if (!string.IsNullOrWhiteSpace(matchId) && requested.Contains(matchId!) && !result.ContainsKey(matchId!))
                    {
                        result[matchId!] = new PersonDisplayDto
                        {
                            Id = matchId!,
                            Name = display.Name,
                            Email = display.Email,
                            Title = display.Title,
                            IsActive = display.IsActive
                        };
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngKeeper GetUsersByIds error ({Count} id)", list.Count);
            return result;
        }
    }

    public async Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetGroupsAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, PersonDisplayDto>(StringComparer.Ordinal);
        var list = (ids ?? Enumerable.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (_httpClient.BaseAddress == null || list.Count == 0)
            return result;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "Group/by-ids")
            {
                Content = JsonContent.Create(new { ids = list })
            };
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MngKeeper GetGroupsByIds failed ({Count} id): HTTP {Status}", list.Count, (int)response.StatusCode);
                return result;
            }

            var envelope = await response.Content.ReadFromJsonAsync<KeeperGroupsByIdsEnvelope>(JsonOptions, cancellationToken);
            var groups = envelope?.Groups;
            if (groups == null || groups.Count == 0)
                return result;

            var requested = new HashSet<string>(list, StringComparer.Ordinal);
            foreach (var g in groups)
            {
                if (string.IsNullOrWhiteSpace(g.GroupId) || !requested.Contains(g.GroupId!) || result.ContainsKey(g.GroupId!))
                    continue;

                result[g.GroupId!] = new PersonDisplayDto
                {
                    Id = g.GroupId!,
                    Name = string.IsNullOrWhiteSpace(g.Name) ? g.GroupId! : g.Name!,
                    IsActive = g.IsActive
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngKeeper GetGroupsByIds error ({Count} id)", list.Count);
            return result;
        }
    }

    public async Task<DomainBrandingDto?> GetDomainByNameAsync(
        string domainName,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null || string.IsNullOrWhiteSpace(domainName))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "Domain");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MngKeeper GetDomains failed: HTTP {Status}", (int)response.StatusCode);
                return null;
            }

            var domains = await response.Content.ReadFromJsonAsync<List<KeeperDomainDto>>(JsonOptions, cancellationToken);
            var match = domains?.FirstOrDefault(d =>
                string.Equals(d.Name, domainName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return null;

            return new DomainBrandingDto
            {
                Name = match.Name,
                DisplayName = match.DisplayName,
                LogoUrl = string.IsNullOrWhiteSpace(match.LogoUrl) ? null : match.LogoUrl.Trim()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngKeeper GetDomainByName error for {DomainName}", domainName);
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
        // by-ids cevabında dolu gelir (tekil GET User/{id} cevabında yoktur, null kalır).
        public string? UserId { get; set; }
        public string? KeycloakUserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Title { get; set; }
        public bool? IsActive { get; set; }
    }

    private sealed class KeeperUsersByIdsEnvelope
    {
        [JsonPropertyName("users")]
        public List<KeeperUserDto>? Users { get; set; }

        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }
    }

    private sealed class KeeperGroupsByIdsEnvelope
    {
        [JsonPropertyName("groups")]
        public List<KeeperGroupDto>? Groups { get; set; }

        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }
    }

    private sealed class KeeperGroupEnvelope
    {
        [JsonPropertyName("group")]
        public KeeperGroupDto? Group { get; set; }

        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }
    }

    private sealed class KeeperGroupDto
    {
        [JsonPropertyName("groupId")]
        public string? GroupId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("isActive")]
        public bool? IsActive { get; set; }
    }

    private sealed class KeeperDomainDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("logoUrl")]
        public string? LogoUrl { get; set; }
    }
}
