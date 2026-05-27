using System.Text.Json;

namespace MngOperations.Application.Utilities;

public static class GroupListParser
{
    public static IReadOnlyList<string> Parse(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array })
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s.Trim());
            }
        }

        return list;
    }

    public static bool Intersects(IReadOnlyList<string> userGroups, IReadOnlyList<string> requiredGroups)
    {
        if (requiredGroups.Count == 0)
            return true;

        if (userGroups.Count == 0)
            return false;

        return userGroups.Any(g => requiredGroups.Contains(g, StringComparer.OrdinalIgnoreCase));
    }
}
