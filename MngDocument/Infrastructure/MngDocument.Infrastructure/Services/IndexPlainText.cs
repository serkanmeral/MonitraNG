using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Index step text extraction. No model and no OCR.
/// </summary>
public static class IndexPlainText
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "docx", "txt", "csv", "md", "markdown", "xlsx", "pptx", "pdf"
    };

    public static bool IsIndexableExtension(string? extension, string? fileName)
    {
        var ext = Normalize(extension);
        if (ext.Length == 0)
            ext = Normalize(Path.GetExtension(fileName));
        return Extensions.Contains(ext);
    }

    public static string ExtractFile(string? extension, string? fileName, byte[] bytes)
    {
        var ext = Normalize(extension);
        if (ext.Length == 0)
            ext = Normalize(Path.GetExtension(fileName));

        return ext switch
        {
            "docx" => ExtractDocx(bytes),
            "txt" or "csv" or "md" or "markdown" => ReadPlain(bytes),
            "xlsx" => ExtractXlsx(bytes),
            "pptx" => ExtractPptx(bytes),
            "pdf" => ExtractPdf(bytes),
            _ => throw new InvalidDataException("Unsupported index file.")
        };
    }

    private static string ExtractDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return DocxStructureParser.ExtractPlainText(stream);
    }

    private static string ReadPlain(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var document = PdfDocument.Open(stream);
        var blocks = new List<string>();
        foreach (var page in document.GetPages())
        {
            var pageText = page.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(pageText))
                blocks.Add(pageText);
        }

        return string.Join("\n", blocks);
    }

    private static string ExtractXlsx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var shared = ReadSharedStrings(zip);
        var sheets = ReadSheets(zip);
        var blocks = new List<string>();
        foreach (var (name, path) in sheets)
        {
            var sheet = LoadXml(zip, path);
            if (sheet?.Root is null)
                continue;

            var lines = new List<string>();
            foreach (var row in sheet.Descendants().Where(e => e.Name.LocalName == "row"))
            {
                var cells = row.Elements()
                    .Where(e => e.Name.LocalName == "c")
                    .Select(cell => (Column: ColumnIndex(Attr(cell, "r")), Text: CellText(cell, shared)))
                    .Where(cell => cell.Text.Length > 0)
                    .OrderBy(cell => cell.Column)
                    .Select(cell => cell.Text);
                var line = string.Join("\t", cells);
                if (line.Length > 0)
                    lines.Add(line);
            }

            if (lines.Count == 0)
                continue;

            blocks.Add(name + "\n" + string.Join("\n", lines));
        }

        return string.Join("\n\n", blocks);
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var doc = LoadXml(zip, "xl/sharedStrings.xml");
        if (doc?.Root is null)
            return new List<string>();

        return doc.Root.Elements()
            .Where(e => e.Name.LocalName == "si")
            .Select(si => string.Concat(si.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)))
            .ToList();
    }

    private static List<(string Name, string Path)> ReadSheets(ZipArchive zip)
    {
        var workbook = LoadXml(zip, "xl/workbook.xml");
        var rels = LoadXml(zip, "xl/_rels/workbook.xml.rels");
        var targets = RelationshipTargets(rels);
        var sheets = new List<(string Name, string Path)>();
        var sheetList = workbook?.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "sheets");
        if (sheetList is not null)
        {
            foreach (var sheet in sheetList.Elements().Where(e => e.Name.LocalName == "sheet"))
            {
                var id = sheet.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
                if (id is null || !targets.TryGetValue(id, out var target))
                    continue;
                var path = ResolvePart("xl/workbook.xml", target);
                sheets.Add((Attr(sheet, "name") ?? "Sheet", path));
            }
        }

        if (sheets.Count > 0)
            return sheets;

        return zip.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => TrailingNumber(e.FullName))
            .Select(e => (Path.GetFileNameWithoutExtension(e.FullName), e.FullName))
            .ToList();
    }

    private static string CellText(XElement cell, IReadOnlyList<string> shared)
    {
        var kind = Attr(cell, "t");
        if (string.Equals(kind, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(cell.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)).Trim();
        }

        var value = cell.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value;
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (string.Equals(kind, "s", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index >= 0
            && index < shared.Count)
        {
            return shared[index].Trim();
        }

        return value.Trim();
    }

    private static string ExtractPptx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var slides = ReadSlides(zip);
        var blocks = new List<string>();
        for (var i = 0; i < slides.Count; i++)
        {
            var body = ParagraphText(LoadXml(zip, slides[i]));
            var notes = ParagraphText(LoadXml(zip, NotesPath(zip, slides[i])));
            if (body.Length == 0 && notes.Length == 0)
                continue;

            var block = new StringBuilder();
            block.Append("Slayt ").Append(i + 1);
            if (body.Length > 0)
            {
                block.Append('\n');
                block.Append(body);
            }

            if (notes.Length > 0)
            {
                block.Append("\nKonuşmacı notu\n");
                block.Append(notes);
            }

            blocks.Add(block.ToString());
        }

        return string.Join("\n\n", blocks);
    }

    private static List<string> ReadSlides(ZipArchive zip)
    {
        var rels = LoadXml(zip, "ppt/_rels/presentation.xml.rels");
        var targets = RelationshipTargets(rels);
        var presentation = LoadXml(zip, "ppt/presentation.xml");
        var idList = presentation?.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "sldIdLst");
        var slides = new List<string>();
        if (idList is not null)
        {
            foreach (var slide in idList.Elements().Where(e => e.Name.LocalName == "sldId"))
            {
                var id = slide.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
                if (id is null || !targets.TryGetValue(id, out var target))
                    continue;
                slides.Add(ResolvePart("ppt/presentation.xml", target));
            }
        }

        if (slides.Count > 0)
            return slides;

        return zip.Entries
            .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                && !e.FullName.Contains("/_rels/", StringComparison.Ordinal))
            .OrderBy(e => TrailingNumber(e.FullName))
            .Select(e => e.FullName)
            .ToList();
    }

    private static string? NotesPath(ZipArchive zip, string slidePath)
    {
        var name = Path.GetFileName(slidePath);
        var dir = Path.GetDirectoryName(slidePath)?.Replace('\\', '/') ?? string.Empty;
        var relsPath = (string.IsNullOrEmpty(dir) ? "_rels/" : dir + "/_rels/") + name + ".rels";
        var rels = LoadXml(zip, relsPath);
        foreach (var rel in rels?.Root?.Elements().Where(e => e.Name.LocalName == "Relationship") ?? Enumerable.Empty<XElement>())
        {
            var type = Attr(rel, "Type") ?? string.Empty;
            var target = Attr(rel, "Target");
            if (target is null || !type.Contains("notesSlide", StringComparison.OrdinalIgnoreCase))
                continue;
            return ResolvePart(slidePath, target);
        }

        return null;
    }

    private static string ParagraphText(XDocument? doc)
    {
        if (doc?.Root is null)
            return string.Empty;

        var lines = new List<string>();
        foreach (var paragraph in doc.Descendants().Where(e => e.Name.LocalName == "p"))
        {
            var text = string.Concat(paragraph.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)).Trim();
            if (text.Length > 0)
                lines.Add(text);
        }

        return string.Join("\n", lines);
    }

    private static Dictionary<string, string> RelationshipTargets(XDocument? rels)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (rels?.Root is null)
            return map;

        foreach (var rel in rels.Root.Elements().Where(e => e.Name.LocalName == "Relationship"))
        {
            var id = Attr(rel, "Id");
            var target = Attr(rel, "Target");
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(target))
                map[id] = target;
        }

        return map;
    }

    private static string ResolvePart(string sourcePart, string target)
    {
        var raw = target.Trim();
        if (raw.StartsWith('/'))
            return raw.TrimStart('/');

        var stack = new List<string>();
        var dir = Path.GetDirectoryName(sourcePart)?.Replace('\\', '/') ?? string.Empty;
        var combined = string.IsNullOrEmpty(dir) ? raw : dir + "/" + raw;
        foreach (var part in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                continue;
            }

            if (part != ".")
                stack.Add(part);
        }

        return string.Join('/', stack);
    }

    private static XDocument? LoadXml(ZipArchive zip, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var entry = zip.GetEntry(path.Replace('\\', '/'));
        if (entry is null)
            return null;
        using var entryStream = entry.Open();
        return XDocument.Load(entryStream);
    }

    private static string? Attr(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;

    private static int ColumnIndex(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef))
            return 0;
        var index = 0;
        foreach (var ch in cellRef)
        {
            if (!char.IsLetter(ch))
                break;
            index = (index * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return index;
    }

    private static int TrailingNumber(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var digits = new string(name.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static string Normalize(string? extension) =>
        (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
}
