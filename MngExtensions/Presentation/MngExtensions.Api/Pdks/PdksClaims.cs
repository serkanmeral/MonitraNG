using System.Security.Claims;
using System.Text.Json;
using MngExtensions.Application.Pdks;

namespace MngExtensions.Api.Pdks;

public static class PdksClaims
{
    public static PdksActor ReadActor(ClaimsPrincipal user)
    {
        var userId = user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;
        var given = user.FindFirst("given_name")?.Value;
        var family = user.FindFirst("family_name")?.Value;
        var username = user.FindFirst("preferred_username")?.Value;
        var display = !string.IsNullOrWhiteSpace(given) ? given.Trim() : username?.Trim() ?? string.Empty;
        if (display.Length > 80)
            display = display[..80];
        var full = string.Join(' ', new[] { display, family?.Trim() }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (full.Length > 120)
            full = full[..120];

        var groups = ReadGroups(user.FindAll("user_groups"));
        return new PdksActor(
            userId.Trim(),
            display,
            user.FindFirst("domain_name")?.Value,
            groups,
            CanManage(user, groups),
            string.IsNullOrWhiteSpace(full) ? display : full);
    }

    private static bool CanManage(ClaimsPrincipal user, IReadOnlyList<string> groups)
    {
        if (IsTrue(user, "is_admin") || IsTrue(user, "is_manager"))
            return true;
        return groups.Any(group =>
            group.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || group.Equals("admins", StringComparison.OrdinalIgnoreCase)
            || group.Equals("manager", StringComparison.OrdinalIgnoreCase)
            || group.Equals("managers", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTrue(ClaimsPrincipal user, string claim)
    {
        var value = user.FindFirst(claim)?.Value;
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadGroups(IEnumerable<Claim> claims)
    {
        var groups = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in claims)
            AddValues(groups, seen, claim.Value);
        return groups;
    }

    private static void AddValues(List<string> groups, HashSet<string> seen, string? raw)
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
                        if (item.ValueKind == JsonValueKind.String)
                            Add(groups, seen, item.GetString());
                    }
                    return;
                }
            }
            catch (JsonException)
            {
            }
        }

        foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Add(groups, seen, part);
    }

    private static void Add(List<string> groups, HashSet<string> seen, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        var trimmed = name.Trim();
        if (seen.Add(trimmed))
            groups.Add(trimmed);
    }
}
