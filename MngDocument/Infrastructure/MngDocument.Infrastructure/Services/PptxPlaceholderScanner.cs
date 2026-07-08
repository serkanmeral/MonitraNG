using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// PPTX slaytlarındaki <c>{{paramKey}}</c> placeholder'larını tarar.
/// </summary>
public static class PptxPlaceholderScanner
{
    private static readonly XNamespace A =
        "http://schemas.openxmlformats.org/drawingml/2006/main";

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed record ScanResult(
        IReadOnlyList<DocxPlaceholderScanner.PlaceholderHit> Placeholders,
        IReadOnlyList<string> Warnings);

    public static ScanResult Scan(Stream pptxStream)
    {
        using var archive = new ZipArchive(pptxStream, ZipArchiveMode.Read, leaveOpen: true);
        var textBuilder = new StringBuilder();

        foreach (var part in archive.Entries.Where(e => IsScannablePart(e.FullName)).OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            using var stream = part.Open();
            textBuilder.Append(ExtractPlainText(stream));
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
                "PPTX içinde '{{' bulundu ancak geçerli scalar placeholder eşleşmedi. " +
                "Placeholder'ı slayt metninde yazın (ör. {{packageNo}}).");
        }

        return new ScanResult(placeholders, warnings);
    }

    public static bool IsScannablePart(string fullName)
    {
        if (!fullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return false;

        if (fullName.StartsWith("ppt/slides/", StringComparison.OrdinalIgnoreCase))
            return true;

        return fullName.StartsWith("ppt/notesSlides/", StringComparison.OrdinalIgnoreCase);
    }

    internal static string ExtractPlainText(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);
        if (doc.Root is null)
            return string.Empty;

        return string.Concat(doc.Root.Descendants(A + "t").Select(t => t.Value));
    }
}
