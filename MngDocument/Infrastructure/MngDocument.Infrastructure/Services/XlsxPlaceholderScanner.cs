using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// XLSX içindeki <c>{{paramKey}}</c> placeholder'larını tarar (inlineStr ve sharedStrings).
/// </summary>
public static class XlsxPlaceholderScanner
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed record ScanResult(
        IReadOnlyList<DocxPlaceholderScanner.PlaceholderHit> Placeholders,
        IReadOnlyList<string> Warnings);

    public static ScanResult Scan(Stream xlsxStream)
    {
        using var archive = new ZipArchive(xlsxStream, ZipArchiveMode.Read, leaveOpen: true);
        var textBuilder = new StringBuilder();

        foreach (var part in archive.Entries.Where(e => IsScannablePart(e.FullName)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            using var stream = part.Open();
            textBuilder.Append(ExtractPlainText(stream, part.FullName));
            textBuilder.Append('\n');
        }

        var fullText = textBuilder.ToString();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in PlaceholderRegex.Matches(fullText))
        {
            var key = match.Groups[1].Value;
            counts.TryGetValue(key, out var n);
            counts[key] = n + 1;
        }

        var placeholders = counts
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new DocxPlaceholderScanner.PlaceholderHit(kv.Key, kv.Value, $"{{{{{kv.Key}}}}}"))
            .ToList();

        var warnings = new List<string>();
        if (fullText.Contains("{{", StringComparison.Ordinal) && placeholders.Count == 0)
        {
            warnings.Add(
                "XLSX içinde '{{' bulundu ancak geçerli scalar placeholder eşleşmedi. " +
                "Placeholder'ı tek hücrede yazın (ör. {{packageNo}}).");
        }

        return new ScanResult(placeholders, warnings);
    }

    public static bool IsScannablePart(string fullName)
    {
        if (!fullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return false;

        if (fullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(fullName, "xl/sharedStrings.xml", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ExtractPlainText(Stream xmlStream, string partName)
    {
        var doc = XDocument.Load(xmlStream);
        if (doc.Root is null)
            return string.Empty;

        if (partName.EndsWith("sharedStrings.xml", StringComparison.OrdinalIgnoreCase))
            return string.Concat(doc.Root.Descendants(Main + "t").Select(t => t.Value));

        return string.Concat(doc.Root.Descendants(Main + "t").Select(t => t.Value));
    }
}
