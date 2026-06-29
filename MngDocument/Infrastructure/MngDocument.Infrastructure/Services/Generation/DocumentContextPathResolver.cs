using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MngDocument.Infrastructure.Services.Generation;

public static class DocumentContextPathResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonObject ToJsonObject(object? record)
    {
        if (record is null)
            return new JsonObject();

        if (record is JsonObject jo)
            return jo;

        var json = JsonSerializer.Serialize(record, JsonOptions);
        return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
    }

    public static string? ExtractRelationId(JsonNode? node)
    {
        if (node is null)
            return null;

        if (node is JsonValue jv)
        {
            var s = jv.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("__dataId", out var dataIdNode))
                return dataIdNode?.ToString()?.Trim();
            if (obj.TryGetPropertyValue("dataId", out var altId))
                return altId?.ToString()?.Trim();
        }

        return null;
    }

    public static JsonNode? GetNode(JsonObject root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return root;

        JsonNode? current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not JsonObject obj)
                return null;

            if (!obj.TryGetPropertyValue(segment, out current))
                return null;
        }

        return current;
    }

    public static string? GetString(JsonObject root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var node = GetNode(root, path);
        if (node is null)
            return null;

        if (node is JsonValue val)
        {
            var s = val.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        if (node is JsonObject)
            return ExtractRelationId(node);

        return node.ToJsonString()?.Trim('"');
    }

    public static string? GetStringWithFallback(JsonObject root, string path, string? fallbackPath)
    {
        var primary = GetString(root, path);
        if (!string.IsNullOrWhiteSpace(primary))
            return primary;

        if (string.IsNullOrWhiteSpace(fallbackPath))
            return null;

        return GetString(root, fallbackPath);
    }

    public static string ApplyFormat(JsonObject root, string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return string.Empty;

        var result = format;
        foreach (Match match in Regex.Matches(format, @"\{([a-zA-Z][a-zA-Z0-9_\.]*)\}"))
        {
            var token = match.Groups[1].Value;
            var value = GetString(root, token) ?? string.Empty;
            result = result.Replace($"{{{token}}}", value, StringComparison.Ordinal);
        }

        return result.Trim();
    }

    public static void SetAtPath(JsonObject root, string path, JsonObject value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return;

        JsonObject current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var seg = segments[i];
            if (!current.TryGetPropertyValue(seg, out var nextNode) || nextNode is not JsonObject nextObj)
            {
                nextObj = new JsonObject();
                current[seg] = nextObj;
            }

            current = nextObj;
        }

        current[segments[^1]] = value;
    }
}
