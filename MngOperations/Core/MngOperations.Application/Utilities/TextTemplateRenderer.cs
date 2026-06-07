using System.Text.Json;
using System.Text.RegularExpressions;

namespace MngOperations.Application.Utilities;

/// <summary>
/// Düz metin şablonları için {{path}} placeholder çözümlemesi (in-app / toaster).
/// </summary>
public static partial class TextTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([^}]+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();

    public static string Render(string template, JsonElement context)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        return PlaceholderRegex().Replace(template, match =>
        {
            var path = match.Groups[1].Value.Trim();
            var pipeIndex = path.IndexOf('|');
            if (pipeIndex >= 0)
                path = path[..pipeIndex].Trim();

            return ResolvePath(context, path) ?? string.Empty;
        });
    }

    private static string? ResolvePath(JsonElement context, string path)
    {
        if (context.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = context;

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
    }
}
