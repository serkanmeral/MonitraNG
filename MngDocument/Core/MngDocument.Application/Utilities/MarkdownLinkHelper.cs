using System.Text.RegularExpressions;

namespace MngDocument.Application.Utilities;

/// <summary>
/// Markdown içindeki Document Intelligence iç linklerini tarar (UI <c>diResourceLink.ts</c> ile uyumlu).
/// </summary>
public static class MarkdownLinkHelper
{
    private const string DiResourcePathPrefix = "/apps/document-intelligence/r/";

    /// <summary>Markdown içeriğinde hedef kaynağa DI iç linki var mı?</summary>
    public static bool ContentLinksToResource(string? content, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(resourceId))
            return false;

        var id = resourceId.Trim();
        if (ContentLinksToId(content, id))
            return true;

        var encoded = Uri.EscapeDataString(id);
        if (!string.Equals(encoded, id, StringComparison.Ordinal))
            return ContentLinksToId(content, encoded);

        return false;
    }

    private static bool ContentLinksToId(string content, string idOrEncoded)
    {
        var pathPattern = Regex.Escape(DiResourcePathPrefix) + Regex.Escape(idOrEncoded) + @"(?:[/?#""')\]\s]|$)";
        if (Regex.IsMatch(content, pathPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return true;

        var queryPattern = @"[?&]resourceId=" + Regex.Escape(idOrEncoded) + @"(?:&|[""')\]\s#]|$)";
        return Regex.IsMatch(content, queryPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
