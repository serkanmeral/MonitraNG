using System.IO.Compression;
using System.Xml.Linq;

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
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("Invalid DOCX: word/document.xml not found.");

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

    public static bool IsDocxExtension(string? extension) =>
        string.Equals(extension?.Trim().TrimStart('.'), "docx", StringComparison.OrdinalIgnoreCase);
}
