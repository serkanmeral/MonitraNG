using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Utilities;

public static class ProfileActionBuilder
{
    public static IReadOnlyList<ProfileActionDto> Build(
        IReadOnlyList<ProfileActionDto> available,
        JsonElement? profileActions)
    {
        if (available.Count == 0)
            return available;

        var presentation = ParsePresentation(profileActions);
        if (presentation.Count == 0)
            return available;

        var byKey = available.ToDictionary(a => a.TransitionKey, StringComparer.OrdinalIgnoreCase);
        var result = new List<ProfileActionDto>();

        foreach (var entry in presentation.OrderBy(e => e.Order))
        {
            if (!byKey.TryGetValue(entry.TransitionKey, out var action))
                continue;

            if (entry.Visible == false)
                continue;

            result.Add(new ProfileActionDto
            {
                TransitionKey = action.TransitionKey,
                Label = entry.Label ?? action.Label,
                FromStateId = action.FromStateId,
                ToStateId = action.ToStateId,
                Enabled = action.Enabled,
                Order = entry.Order,
                RequiredFields = action.RequiredFields
            });
        }

        return result;
    }

    private static IReadOnlyList<PresentationEntry> ParsePresentation(JsonElement? profileActions)
    {
        if (profileActions is not { ValueKind: JsonValueKind.Object or JsonValueKind.Array })
            return Array.Empty<PresentationEntry>();

        if (profileActions.Value.ValueKind == JsonValueKind.Array)
            return ParseArray(profileActions.Value);

        if (profileActions.Value.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            return ParseArray(items);

        return ParseObjectMap(profileActions.Value);
    }

    private static IReadOnlyList<PresentationEntry> ParseArray(JsonElement array)
    {
        var entries = new List<PresentationEntry>();
        var order = 0;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var key = item.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    entries.Add(new PresentationEntry(key, order++, null, true));
                }

                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var transitionKey = ReadString(item, "transitionKey") ?? ReadString(item, "key");
            if (string.IsNullOrWhiteSpace(transitionKey))
                continue;

            var entryOrder = ReadInt(item, "order") ?? order++;
            entries.Add(new PresentationEntry(
                transitionKey,
                entryOrder,
                ReadString(item, "label"),
                ReadBool(item, "visible", true)));
        }

        return entries;
    }

    private static IReadOnlyList<PresentationEntry> ParseObjectMap(JsonElement obj)
    {
        var entries = new List<PresentationEntry>();

        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                entries.Add(new PresentationEntry(
                    prop.Name,
                    ReadInt(prop.Value, "order") ?? entries.Count,
                    ReadString(prop.Value, "label"),
                    ReadBool(prop.Value, "visible", true)));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                entries.Add(new PresentationEntry(prop.Name, prop.Value.GetInt32(), null, true));
            }
            else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
            {
                entries.Add(new PresentationEntry(prop.Name, entries.Count, null, prop.Value.GetBoolean()));
            }
        }

        return entries;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return null;

        return prop.TryGetInt32(out var value) ? value : null;
    }

    private static bool ReadBool(JsonElement element, string name, bool defaultValue)
    {
        if (!element.TryGetProperty(name, out var prop))
            return defaultValue;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    private sealed record PresentationEntry(string TransitionKey, int Order, string? Label, bool Visible);
}
