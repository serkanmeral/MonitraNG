using System.Text.Json;
using System.Text.RegularExpressions;

namespace MngOperations.Application.Utilities;

public static partial class HtmlRichTextHelper
{
    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    public static bool IsEffectivelyEmpty(object? value) =>
        value switch
        {
            null => true,
            string s => IsEffectivelyEmptyHtml(s),
            JsonElement el when el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonElement el when el.ValueKind == JsonValueKind.String => IsEffectivelyEmptyHtml(el.GetString()),
            _ => false
        };

    public static bool IsEffectivelyEmptyHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return true;

        var stripped = HtmlTagRegex().Replace(html, " ")
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return stripped.Length == 0;
    }

    public static string? StripToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var stripped = HtmlTagRegex().Replace(html, " ")
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
        return stripped.Length == 0 ? null : stripped;
    }
}
