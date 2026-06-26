using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// DOCX içindeki <c>{{paramKey}}</c> ifadelerini değerlerle değiştirir (LibreOffice şablon modeli).
/// </summary>
public static class DocxPlaceholderMerger
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static byte[] Merge(byte[] docxBytes, IReadOnlyDictionary<string, string> values)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();

        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
            {
                var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream = entry.Open();
                using var outStream = newEntry.Open();

                if (DocxPlaceholderScanner.IsScannablePart(entry.FullName))
                {
                    var doc = XDocument.Load(inStream);
                    MergeInXmlDocument(doc, values);
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

    internal static void MergeInXmlDocument(XDocument doc, IReadOnlyDictionary<string, string> values)
    {
        foreach (var textNode in doc.Descendants(W + "t").ToList())
        {
            var replaced = ReplaceTokens(textNode.Value, values);
            if (replaced != textNode.Value)
                textNode.Value = replaced;
        }

        foreach (var paragraph in doc.Descendants(W + "p").ToList())
            NormalizeSplitPlaceholdersInParagraph(paragraph, values);
    }

    private static void NormalizeSplitPlaceholdersInParagraph(
        XElement paragraph,
        IReadOnlyDictionary<string, string> values)
    {
        var textNodes = paragraph.Descendants(W + "t").ToList();
        if (textNodes.Count == 0)
            return;

        var combined = string.Concat(textNodes.Select(t => t.Value));
        var replaced = ReplaceTokens(combined, values);
        if (combined == replaced)
            return;

        textNodes[0].Value = replaced;
        for (var i = 1; i < textNodes.Count; i++)
            textNodes[i].Value = string.Empty;
    }

    private static string ReplaceTokens(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
            return text;

        return PlaceholderRegex.Replace(text, match =>
        {
            var key = match.Groups[1].Value;
            if (values.TryGetValue(key, out var value))
                return value ?? string.Empty;

            foreach (var kv in values)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value ?? string.Empty;
            }

            return match.Value;
        });
    }
}
