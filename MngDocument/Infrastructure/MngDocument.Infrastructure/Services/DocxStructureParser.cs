using System.IO.Compression;
using System.Xml.Linq;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>DOCX (OOXML) gövde paragraflarını basit XML parse ile çıkarır.</summary>
public static class DocxStructureParser
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public sealed record ParseResult(
        IReadOnlyList<(int Index, string Text)> Paragraphs,
        int TableCount);

    public static ParseResult Parse(Stream docxStream)
    {
        using var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = DocxZipHelper.GetRequiredEntry(archive, "word/document.xml");

        using var entryStream = entry.Open();
        var doc = XDocument.Load(entryStream);
        var body = doc.Root?.Element(W + "body")
            ?? throw new InvalidOperationException("Invalid DOCX: body element not found.");

        var paragraphs = new List<(int Index, string Text)>();
        var paragraphIndex = 0;
        var tableCount = 0;

        foreach (var element in body.Elements())
        {
            if (element.Name == W + "tbl")
            {
                tableCount++;
                continue;
            }

            if (element.Name != W + "p")
                continue;

            var text = string.Concat(element.Descendants(W + "t").Select(t => t.Value)).Trim();
            if (text.Length > 0)
                paragraphs.Add((paragraphIndex, text));
            paragraphIndex++;
        }

        return new ParseResult(paragraphs, tableCount);
    }

    /// <summary>Paragraf ve tablo hücrelerindeki metni, arama indeksi için düz metin olarak birleştirir.</summary>
    public static string ExtractPlainText(Stream docxStream)
    {
        using var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = DocxZipHelper.GetRequiredEntry(archive, "word/document.xml");
        using var entryStream = entry.Open();
        var doc = XDocument.Load(entryStream);
        var body = doc.Root?.Element(W + "body")
            ?? throw new InvalidOperationException("Invalid DOCX: body element not found.");

        var blocks = new List<string>();
        foreach (var element in body.Elements())
        {
            if (element.Name == W + "p")
            {
                var text = string.Concat(element.Descendants(W + "t").Select(t => t.Value)).Trim();
                if (text.Length > 0)
                    blocks.Add(text);
                continue;
            }

            if (element.Name != W + "tbl")
                continue;

            foreach (var row in element.Elements(W + "tr"))
            {
                var cells = row.Elements(W + "tc")
                    .Select(cell => string.Concat(cell.Descendants(W + "t").Select(t => t.Value)).Trim())
                    .Where(cell => cell.Length > 0);
                var line = string.Join("\t", cells);
                if (line.Length > 0)
                    blocks.Add(line);
            }
        }

        return string.Join("\n", blocks);
    }

    public static bool IsDocxExtension(string? extension) =>
        string.Equals(extension?.Trim().TrimStart('.'), "docx", StringComparison.OrdinalIgnoreCase);

    public static bool IsXlsxExtension(string? extension) =>
        string.Equals(extension?.Trim().TrimStart('.'), "xlsx", StringComparison.OrdinalIgnoreCase);

    public static bool IsPptxExtension(string? extension) =>
        string.Equals(extension?.Trim().TrimStart('.'), "pptx", StringComparison.OrdinalIgnoreCase);

    /// <summary>DOCX / XLSX / PPTX şablon kaynağı (G5 XLSX pilot).</summary>
    public static bool IsTemplateSourceExtension(string? extension) =>
        ManagedOfficeProfiles.IsManagedOfficeExtension(extension, null);
}
