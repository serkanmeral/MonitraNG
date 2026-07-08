using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// XLSX içindeki <c>{{paramKey}}</c> ifadelerini değerlerle değiştirir.
/// </summary>
public static class XlsxPlaceholderMerger
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static byte[] Merge(
        byte[] xlsxBytes,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? preservePlaceholderKeys = null)
    {
        using var input = new MemoryStream(xlsxBytes, writable: false);
        using var output = new MemoryStream();

        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
            {
                var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream = entry.Open();
                using var outStream = newEntry.Open();

                if (XlsxPlaceholderScanner.IsScannablePart(entry.FullName))
                {
                    var doc = XDocument.Load(inStream);
                    MergeInXmlDocument(doc, values, preservePlaceholderKeys);
                    doc.Save(outStream);
                }
                else
                {
                    inStream.CopyTo(outStream);
                }
            }
        }

        return output.ToArray();
    }

    internal static void MergeInXmlDocument(
        XDocument doc,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? preservePlaceholderKeys = null)
    {
        foreach (var textNode in doc.Descendants(Main + "t").ToList())
        {
            var replaced = ReplaceTokens(textNode.Value, values, preservePlaceholderKeys);
            if (replaced != textNode.Value)
                textNode.Value = replaced;
        }
    }

    private static string ReplaceTokens(
        string text,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? preservePlaceholderKeys)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
            return text;

        return PlaceholderRegex.Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            if (ShouldPreservePlaceholder(key, preservePlaceholderKeys))
                return match.Value;

            if (values.TryGetValue(key, out var value))
                return value;

            foreach (var kv in values)
            {
                if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                return kv.Value;
            }

            return match.Value;
        });
    }

    private static bool ShouldPreservePlaceholder(string key, IReadOnlySet<string>? preservePlaceholderKeys)
    {
        if (preservePlaceholderKeys is null || preservePlaceholderKeys.Count == 0)
            return false;

        if (preservePlaceholderKeys.Contains(key))
            return true;

        foreach (var candidate in preservePlaceholderKeys)
        {
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
