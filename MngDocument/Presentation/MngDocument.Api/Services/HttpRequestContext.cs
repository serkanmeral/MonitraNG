using System.Security.Claims;
using System.Text.Json;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Services;

/// <summary>
/// JWT claim'lerinden çağıran kimliği (gateway forward Bearer). DG ile aynı claim uzayı.
/// </summary>
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

    public string? DisplayName =>
        User?.FindFirst("name")?.Value
        ?? User?.FindFirst(ClaimTypes.GivenName)?.Value
        ?? Username;

    public string? DomainId => User?.FindFirst("domain_id")?.Value;

    public string? DomainName => User?.FindFirst("domain_name")?.Value;

    public IReadOnlyList<string> UserGroups => ReadGroups(User?.FindAll("user_groups"));

    public bool IsAdmin => ReadBoolClaim("isAdmin") || ReadBoolClaim("is_admin");

    public bool IsManager =>
        IsAdmin || ReadBoolClaim("isManager") || ReadBoolClaim("is_manager");

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

    private static IReadOnlyList<string> ReadGroups(IEnumerable<Claim>? claims)
    {
        if (claims == null)
            return Array.Empty<string>();

        var groups = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in claims)
            AddGroupValues(groups, seen, claim.Value);

        return groups;
    }

    private static void AddGroupValues(List<string> groups, HashSet<string> seen, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                            continue;
                        AddGroup(groups, seen, item.GetString());
                    }
                    return;
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            AddGroup(groups, seen, part);
    }

    private static void AddGroup(List<string> groups, HashSet<string> seen, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var trimmed = name.Trim();
        if (seen.Add(trimmed))
            groups.Add(trimmed);
    }
}
