namespace MngOperations.Application.Utilities;

/// <summary>
/// Sprint ilk dilimi. Üst iş sprinte girmez. Kapatmak işin durumunu değiştirmez.
/// </summary>
public static class SprintRules
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Closed = "closed";
    public const string ScopeAll = "all";
    public const string ScopeBacklog = "backlog";
    public const string EpicPackKey = "type.epic";
    public const int MaxNameLength = 80;
    public const int MaxGoalLength = 500;

    public static string NormalizeState(string? state)
    {
        var value = state?.Trim().ToLowerInvariant();
        return value switch
        {
            Active => Active,
            Closed => Closed,
            _ => Planned
        };
    }

    public static bool CanJoin(IReadOnlyDictionary<string, object?>? type)
    {
        if (type == null)
            return false;

        var packKey = WorkItemDataHelper.GetString(type, "packKey");
        if (string.Equals(packKey, EpicPackKey, StringComparison.OrdinalIgnoreCase))
            return false;

        return WorkItemDataHelper.GetStringList(type, "childTypeKeys").Count == 0;
    }

    public static bool TryParseDate(string? raw, out string iso)
    {
        iso = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", out var date)
            && !DateOnly.TryParse(raw.Trim(), out date))
        {
            return false;
        }

        iso = date.ToString("yyyy-MM-dd");
        return true;
    }
}
