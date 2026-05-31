using System.Security.Claims;
using System.Text.Json;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Services;

public class HttpRequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirst("sub")?.Value ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // mng_person_id claim'i bazı ortamlarda temiz id yerine tüm @users kaydının JSON'u olarak
    // gelebiliyor; bu durumda gerçek id'yi (__dataId/id) çıkarırız (yoksa sub'a düşeriz).
    public string? MngPersonId
    {
        get
        {
            var extracted = ExtractPersonId(User?.FindFirst("mng_person_id")?.Value);
            return !string.IsNullOrWhiteSpace(extracted) ? extracted : UserId;
        }
    }

    private static string? ExtractPersonId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Trim();
        if (trimmed.Length == 0 || trimmed[0] != '{') return trimmed; // zaten düz id

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            foreach (var name in new[] { "__dataId", "_id", "id", "userId", "sub" })
            {
                if (root.TryGetProperty(name, out var el)
                    && el.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(el.GetString()))
                {
                    return el.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // JSON değilmiş → ham değeri döndür (çözülmese de patlamaz).
        }

        return trimmed;
    }

    public string? Username =>
        User?.FindFirst("preferred_username")?.Value ?? User?.FindFirst(ClaimTypes.Name)?.Value;

    public string? DomainId => User?.FindFirst("domain_id")?.Value;

    public string? DomainName => User?.FindFirst("domain_name")?.Value;

    public IReadOnlyList<string> UserGroups
    {
        get
        {
            var raw = User?.FindFirst("user_groups")?.Value;
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public bool IsAdmin => ReadBoolClaim("isAdmin") || ReadBoolClaim("is_admin");

    public bool IsManager => ReadBoolClaim("isManager") || ReadBoolClaim("is_manager");

    public string? BearerToken
    {
        get
        {
            var auth = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;
            return auth["Bearer ".Length..].Trim();
        }
    }

    private bool ReadBoolClaim(string claimType) =>
        string.Equals(User?.FindFirst(claimType)?.Value, "true", StringComparison.OrdinalIgnoreCase);
}
