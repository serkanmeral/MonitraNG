using System.Text.Json;

namespace MngOperations.Application.Utilities;

public static class FormLayoutHelper
{
    public static IReadOnlyList<string> ExtractOrderedFieldKeys(JsonElement? layout)
    {
        if (layout is not { ValueKind: JsonValueKind.Object })
            return Array.Empty<string>();

        if (!layout.Value.TryGetProperty("sections", out var sections)
            || sections.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sections.EnumerateArray())
        {
            if (section.ValueKind != JsonValueKind.Object)
                continue;

            if (!section.TryGetProperty("fields", out var fields)
                || fields.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var field in fields.EnumerateArray())
            {
                var key = field.ValueKind == JsonValueKind.String
                    ? field.GetString()
                    : field.ToString();

                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                    continue;

                keys.Add(key);
            }
        }

        return keys;
    }
}
