using System.Security.Claims;
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

    public bool IsAdmin => string.Equals(User?.FindFirst("isAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    public bool IsManager => string.Equals(User?.FindFirst("isManager")?.Value, "true", StringComparison.OrdinalIgnoreCase);

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
}
