using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// DOCX içindeki <c>{{paramKey}}</c> placeholder'larını tarar (LibreOffice/Word şablon modeli).
/// Metin, OOXML parçalarındaki w:t düğümlerinin sırasıyla birleştirilmesiyle çıkarılır.
/// </summary>
public static class DocxPlaceholderScanner
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Placeholder anahtarı: harf ile başlar, harf/rakam/alt çizgi.</summary>
    private static readonly Regex PlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed record ScanResult(
        IReadOnlyList<PlaceholderHit> Placeholders,
        IReadOnlyList<string> Warnings);

    public sealed record PlaceholderHit(string Key, int OccurrenceCount, string Token);

    public static ScanResult Scan(Stream docxStream)
    {
        using var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true);
        var parts = archive.Entries
            .Where(e => IsScannablePart(e.FullName))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();

        var textBuilder = new StringBuilder();
        foreach (var part in parts)
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
            .Select(kv => new PlaceholderHit(kv.Key, kv.Value, $"{{{{{kv.Key}}}}}"))
            .ToList();

        var warnings = new List<string>();
        if (fullText.Contains("{{", StringComparison.Ordinal) && placeholders.Count == 0)
        {
            warnings.Add(
                "DOCX içinde '{{' bulundu ancak geçerli placeholder eşleşmedi. " +
                "Placeholder'ı tek parça yazın (ör. {{musteriAdi}}); Word/LibreOffice run bölmesi sorun çıkarabilir.");
        }

        var orphanOpen = CountUnmatchedOpens(fullText);
        if (orphanOpen > 0)
        {
            warnings.Add(
                $"{orphanOpen} adet tamamlanmamış '{{' ifadesi bulundu. Placeholder sözdizimini kontrol edin.");
        }

        return new ScanResult(placeholders, warnings);
    }

    public static bool IsScannablePart(string fullName)
    {
        if (!fullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(fullName, "word/document.xml", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)
            && fullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return true;

        if (fullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase)
            && fullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string ExtractPlainText(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);
        if (doc.Root is null)
            return string.Empty;

        return string.Concat(doc.Root.Descendants(W + "t").Select(t => t.Value));
    }

    private static int CountUnmatchedOpens(string text)
    {
        var open = 0;
        var orphan = 0;
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '{')
                continue;
            if (text[i + 1] == '{')
            {
                open++;
                i++;
                continue;
            }

            if (open > 0)
                orphan += open;
            open = 0;
        }

        if (open > 0)
            orphan += open;

        return orphan;
    }
}
