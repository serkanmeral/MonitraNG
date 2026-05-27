namespace MngOperations.Application.Utilities;

public sealed class QueryResolveContext
{
    public string? WorkspaceId { get; init; }
    public string? BoardId { get; init; }
    public string? Username { get; init; }
    public DateTime UtcNow { get; init; } = DateTime.UtcNow;
}

public static class QueryParameterResolver
{
    public static Dictionary<string, object?> Resolve(
        IReadOnlyDictionary<string, object?> raw,
        QueryResolveContext context)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in raw)
            result[key] = ResolveValue(value, context);
        return result;
    }

    private static object? ResolveValue(object? value, QueryResolveContext context)
    {
        if (value is not string s)
            return value;

        return s switch
        {
            "{{currentUser}}" => context.Username,
            "{{currentWorkspace}}" => context.WorkspaceId,
            "{{currentBoard}}" => context.BoardId,
            "{{today}}" => context.UtcNow.Date,
            "{{startOfWeek}}" => StartOfWeek(context.UtcNow),
            "{{now}}" or "{{asOf}}" => context.UtcNow,
            _ when s.StartsWith("{{", StringComparison.Ordinal) && s.EndsWith("}}", StringComparison.Ordinal)
                => ResolveToken(s[2..^2].Trim(), context),
            _ => s
        };
    }

    private static object? ResolveToken(string token, QueryResolveContext context) =>
        token.ToLowerInvariant() switch
        {
            "currentuser" => context.Username,
            "currentworkspace" => context.WorkspaceId,
            "currentboard" => context.BoardId,
            "today" => context.UtcNow.Date,
            "startofweek" => StartOfWeek(context.UtcNow),
            "now" or "asof" => context.UtcNow,
            _ => $"{{{{{token}}}}}"
        };

    private static DateTime StartOfWeek(DateTime utc)
    {
        var diff = (7 + (utc.DayOfWeek - DayOfWeek.Monday)) % 7;
        return utc.Date.AddDays(-diff);
    }
}
