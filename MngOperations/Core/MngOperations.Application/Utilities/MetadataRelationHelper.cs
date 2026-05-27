using System.Text.Json;

namespace MngOperations.Application.Utilities;

public static class MetadataRelationHelper
{
    public static IReadOnlyList<string> ParseIdList(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Array })
            return Array.Empty<string>();

        var ids = new List<string>();
        foreach (var item in element.Value.EnumerateArray())
        {
            var id = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("__dataId", out var dataId) => dataId.GetString(),
                JsonValueKind.Object when item.TryGetProperty("dataId", out var altId) => altId.GetString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id);
        }

        return ids;
    }
}
