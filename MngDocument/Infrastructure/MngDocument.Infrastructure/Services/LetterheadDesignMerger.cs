using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>Copies header parts from a letterhead design DOCX onto a target document.</summary>
public static class LetterheadDesignMerger
{
    private static readonly XNamespace RelNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly XNamespace W = DocxSectPrHelper.W;
    private static readonly XNamespace R = DocxSectPrHelper.R;

    private const string TargetHeaderPartPath = "word/header1.xml";
    private const string TargetHeaderRelsPath = "word/_rels/header1.xml.rels";

    private sealed record PrimaryHeaderSelection(string SourceHeaderPartPath, string? SourceHeaderRelsPath);

    /// <summary>Applies design header parts and copies missing embedded media.</summary>
    public static byte[] EnsureHeaderWithMediaFromDesign(byte[] targetDocxBytes, byte[] designDocxBytes)
    {
        var merged = ApplyHeader(targetDocxBytes, designDocxBytes);
        return RepairHeaderMediaFromDesign(merged, designDocxBytes);
    }

    public static byte[] ApplyHeader(byte[] targetDocxBytes, byte[] designDocxBytes)
    {
        using var designInput = new MemoryStream(designDocxBytes, writable: false);
        using var designArchive = new ZipArchive(designInput, ZipArchiveMode.Read, leaveOpen: true);

        var selection = ResolvePrimaryHeader(designArchive);
        if (selection is null)
            return targetDocxBytes;

        var partsToCopy = CollectDesignParts(designArchive, selection);
        if (partsToCopy.Count == 0)
            return targetDocxBytes;

        using var targetInput = new MemoryStream(targetDocxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(targetInput, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = ReadEntryText(readArchive, "word/document.xml");

            foreach (var entry in readArchive.Entries)
            {
                if (IsReplacedPart(entry.FullName))
                    continue;
                CopyEntry(entry, writeArchive);
            }

            foreach (var (sourcePath, targetPath) in partsToCopy)
            {
                var entry = DocxZipHelper.GetRequiredEntry(designArchive, sourcePath);
                CopyEntryToPath(entry, writeArchive, targetPath);
            }

            WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildDocumentRels(readArchive));
            WriteEntry(writeArchive, "word/document.xml", InjectHeaderReference(documentXml));
            WriteEntry(writeArchive, "[Content_Types].xml", BuildContentTypes(readArchive, partsToCopy));
        }

        return output.ToArray();
    }

    private const string TargetFooterPartPath = "word/footer1.xml";
    private const string TargetFooterRelsPath = "word/_rels/footer1.xml.rels";

    public static byte[] ApplyFooter(byte[] targetDocxBytes, byte[] designDocxBytes)
    {
        using var designInput = new MemoryStream(designDocxBytes, writable: false);
        using var designArchive = new ZipArchive(designInput, ZipArchiveMode.Read, leaveOpen: true);

        var selection = ResolvePrimaryFooter(designArchive);
        if (selection is null)
            return targetDocxBytes;

        var partsToCopy = CollectFooterDesignParts(designArchive, selection);
        if (partsToCopy.Count == 0)
            return targetDocxBytes;

        using var targetInput = new MemoryStream(targetDocxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(targetInput, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = ReadEntryText(readArchive, "word/document.xml");

            foreach (var entry in readArchive.Entries)
            {
                if (IsReplacedFooterPart(entry.FullName))
                    continue;
                CopyEntry(entry, writeArchive);
            }

            foreach (var (sourcePath, targetPath) in partsToCopy)
            {
                var entry = DocxZipHelper.GetRequiredEntry(designArchive, sourcePath);
                CopyEntryToPath(entry, writeArchive, targetPath);
            }

            WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildFooterDocumentRels(readArchive));
            WriteEntry(writeArchive, "word/document.xml", InjectFooterReference(documentXml));
            WriteEntry(writeArchive, "[Content_Types].xml", BuildFooterContentTypes(readArchive, partsToCopy));
        }

        return output.ToArray();
    }

    public static bool HasDesignFooter(byte[] designDocxBytes)
    {
        using var input = new MemoryStream(designDocxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var selection = ResolvePrimaryFooter(archive);
        if (selection is null)
            return false;

        return MeasureFooterTextLength(archive, selection.SourceFooterPartPath) > 0;
    }

    /// <summary>Footer part exists in design DOCX (empty table counts).</summary>
    public static bool HasFooterTableStructure(byte[] designDocxBytes)
    {
        using var input = new MemoryStream(designDocxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (DocxZipHelper.GetEntry(archive, TargetFooterPartPath) is null)
            return false;

        if (DocxZipHelper.GetEntry(archive, "word/document.xml") is null)
            return false;

        var documentXml = ReadEntryText(archive, "word/document.xml");
        return documentXml.Contains("footerReference", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Header part linked from document body (skeleton or user-edited).</summary>
    public static bool HasDesignHeader(byte[] designDocxBytes)
    {
        using var input = new MemoryStream(designDocxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (DocxZipHelper.GetEntry(archive, TargetHeaderPartPath) is null)
            return false;

        if (DocxZipHelper.GetEntry(archive, "word/document.xml") is null)
            return false;

        var documentXml = ReadEntryText(archive, "word/document.xml");
        return documentXml.Contains("headerReference", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasAppliedFooter(byte[] docxBytes)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var entry = DocxZipHelper.GetEntry(archive, TargetFooterPartPath);
        if (entry is null)
            return false;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xml = reader.ReadToEnd();
        return Regex.IsMatch(xml, "<w:t[^>]*>\\s*\\S", RegexOptions.CultureInvariant);
    }

    private sealed record PrimaryFooterSelection(string SourceFooterPartPath, string? SourceFooterRelsPath);

    private static PrimaryFooterSelection? ResolvePrimaryFooter(ZipArchive designArchive)
    {
        var footerParts = designArchive.Entries
            .Select(e => DocxZipHelper.NormalizeEntryPath(e.FullName))
            .Where(p => p.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase)
                        && p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (footerParts.Count == 0)
            return null;

        var preferred = TryResolveDefaultFooterPart(designArchive);
        if (preferred is not null
            && footerParts.Contains(preferred, StringComparer.OrdinalIgnoreCase)
            && !IsFooterEmpty(designArchive, preferred))
        {
            return CreateFooterSelection(preferred);
        }

        var richest = footerParts
            .OrderByDescending(part => MeasureFooterTextLength(designArchive, part))
            .First();

        if (MeasureFooterTextLength(designArchive, richest) == 0)
            return CreateFooterSelection(footerParts[0]);

        return CreateFooterSelection(richest);
    }

    private static PrimaryFooterSelection CreateFooterSelection(string footerPartPath)
    {
        var relsPath = ResolveFooterRelsPath(footerPartPath);
        return new PrimaryFooterSelection(footerPartPath, relsPath);
    }

    private static string? TryResolveDefaultFooterPart(ZipArchive designArchive)
    {
        if (DocxZipHelper.GetEntry(designArchive, "word/document.xml") is null
            || DocxZipHelper.GetEntry(designArchive, "word/_rels/document.xml.rels") is null)
            return null;

        var documentXml = ReadEntryText(designArchive, "word/document.xml");
        var relsXml = ReadEntryText(designArchive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(documentXml);
        var rels = XDocument.Parse(relsXml);

        var defaultRef = doc.Descendants(W + "footerReference")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute(W + "type"), "default", StringComparison.OrdinalIgnoreCase))
            ?? doc.Descendants(W + "footerReference").FirstOrDefault();

        var relId = (string?)defaultRef?.Attribute(R + "id");
        if (string.IsNullOrWhiteSpace(relId))
            return null;

        var rel = rels.Root?.Elements(RelNs + "Relationship")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("Id"), relId, StringComparison.OrdinalIgnoreCase));

        var target = (string?)rel?.Attribute("Target");
        if (string.IsNullOrWhiteSpace(target))
            return null;

        return DocxZipHelper.NormalizeEntryPath($"word/{target.Replace('\\', '/')}");
    }

    private static List<(string SourcePath, string TargetPath)> CollectFooterDesignParts(
        ZipArchive designArchive,
        PrimaryFooterSelection selection)
    {
        var parts = new List<(string SourcePath, string TargetPath)>
        {
            (selection.SourceFooterPartPath, TargetFooterPartPath)
        };

        if (!string.IsNullOrWhiteSpace(selection.SourceFooterRelsPath)
            && DocxZipHelper.GetEntry(designArchive, selection.SourceFooterRelsPath) is not null)
        {
            parts.Add((selection.SourceFooterRelsPath, TargetFooterRelsPath));
            var relsText = ReadEntryText(designArchive, selection.SourceFooterRelsPath);
            foreach (var mediaPath in ExtractRelationshipTargets(relsText, "media/"))
            {
                var normalized = DocxZipHelper.NormalizeEntryPath($"word/{mediaPath}");
                parts.Add((normalized, normalized));
            }
        }

        return parts
            .GroupBy(p => p.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string? ResolveFooterRelsPath(string footerPartPath)
    {
        var fileName = Path.GetFileName(footerPartPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return $"word/_rels/{fileName}.rels";
    }

    private static int MeasureFooterTextLength(ZipArchive archive, string footerPartPath)
    {
        var entry = DocxZipHelper.GetEntry(archive, footerPartPath);
        if (entry is null)
            return 0;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xml = reader.ReadToEnd();
        return Regex.Matches(xml, "<w:t[^>]*>([^<]*)</w:t>", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Sum(m => m.Groups[1].Value.Trim().Length);
    }

    private static bool IsFooterEmpty(ZipArchive archive, string footerPartPath) =>
        MeasureFooterTextLength(archive, footerPartPath) == 0;

    private static bool IsReplacedFooterPart(string path)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        return normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(TargetFooterPartPath, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(TargetFooterRelsPath, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFooterDocumentRels(ZipArchive targetArchive)
    {
        var existing = ReadEntryText(targetArchive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid document rels.");

        root.Elements(RelNs + "Relationship")
            .Where(r => string.Equals((string?)r.Attribute("Target"), "footer1.xml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals((string?)r.Attribute("Id"), FooterInjector.FooterReferenceRelId, StringComparison.OrdinalIgnoreCase))
            .Remove();

        root.Add(new XElement(RelNs + "Relationship",
            new XAttribute("Id", FooterInjector.FooterReferenceRelId),
            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer"),
            new XAttribute("Target", "footer1.xml")));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string InjectFooterReference(string documentXml) =>
        DocxSectPrHelper.UpsertSectionReferences(
            documentXml,
            includeHeader: false,
            headerRelId: LetterheadInjector.HeaderReferenceRelId,
            includeFooter: true,
            footerRelId: FooterInjector.FooterReferenceRelId);

    private static string BuildFooterContentTypes(
        ZipArchive targetArchive,
        IReadOnlyList<(string SourcePath, string TargetPath)> copiedParts)
    {
        var existing = ReadEntryText(targetArchive, "[Content_Types].xml");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid content types.");

        RemoveOverride(root, "/word/footer1.xml");
        AddOverride(root, "/word/footer1.xml",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml");

        foreach (var (_, targetPath) in copiedParts)
        {
            var normalized = DocxZipHelper.NormalizeEntryPath(targetPath);
            if (!normalized.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase))
                continue;

            var partName = "/" + normalized;
            RemoveOverride(root, partName);
            var extension = Path.GetExtension(normalized).TrimStart('.');
            if (string.Equals(extension, "png", StringComparison.OrdinalIgnoreCase))
                AddDefault(root, "png", "image/png");
            else if (string.Equals(extension, "jpeg", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(extension, "jpg", StringComparison.OrdinalIgnoreCase))
                AddDefault(root, "jpeg", "image/jpeg");

            AddOverride(root, partName, ResolveMediaContentType(extension));
        }

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    internal static bool HasAppliedHeader(byte[] docxBytes)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var entry = DocxZipHelper.GetEntry(archive, TargetHeaderPartPath);
        if (entry is null)
            return false;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xml = reader.ReadToEnd();
        return HeaderXmlHasContent(xml);
    }

    /// <summary>Header references an embedded image that is missing from the package.</summary>
    internal static bool HasBrokenHeaderImages(byte[] docxBytes)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var headerEntry = DocxZipHelper.GetEntry(archive, TargetHeaderPartPath);
        if (headerEntry is null)
            return false;

        using var stream = headerEntry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var headerXml = reader.ReadToEnd();
        if (!headerXml.Contains("a:blip", StringComparison.OrdinalIgnoreCase)
            && !headerXml.Contains(":blip", StringComparison.OrdinalIgnoreCase))
            return false;

        var relsEntry = DocxZipHelper.GetEntry(archive, TargetHeaderRelsPath);
        if (relsEntry is null)
            return true;

        using var relsStream = relsEntry.Open();
        using var relsReader = new StreamReader(relsStream, Encoding.UTF8);
        var relsXml = relsReader.ReadToEnd();

        foreach (var mediaPath in ExtractRelationshipTargets(relsXml, "media/"))
        {
            var normalized = DocxZipHelper.NormalizeEntryPath($"word/{mediaPath}");
            if (DocxZipHelper.GetEntry(archive, normalized) is null)
                return true;
        }

        return false;
    }

    /// <summary>Copies missing header-linked media parts from a letterhead design DOCX.</summary>
    internal static byte[] RepairHeaderMediaFromDesign(byte[] docxBytes, byte[] designDocxBytes)
    {
        if (!HasBrokenHeaderImages(docxBytes))
            return docxBytes;

        using var designInput = new MemoryStream(designDocxBytes, writable: false);
        using var designArchive = new ZipArchive(designInput, ZipArchiveMode.Read, leaveOpen: true);

        var mediaToCopy = new List<string>();
        using (var targetInput = new MemoryStream(docxBytes, writable: false))
        using (var targetArchive = new ZipArchive(targetInput, ZipArchiveMode.Read, leaveOpen: true))
        {
            var relsEntry = DocxZipHelper.GetEntry(targetArchive, TargetHeaderRelsPath);
            if (relsEntry is null)
                return docxBytes;

            using var relsStream = relsEntry.Open();
            using var relsReader = new StreamReader(relsStream, Encoding.UTF8);
            foreach (var mediaPath in ExtractRelationshipTargets(relsReader.ReadToEnd(), "media/"))
            {
                var normalized = DocxZipHelper.NormalizeEntryPath($"word/{mediaPath}");
                if (DocxZipHelper.GetEntry(targetArchive, normalized) is null
                    && DocxZipHelper.GetEntry(designArchive, normalized) is not null)
                    mediaToCopy.Add(normalized);
            }
        }

        if (mediaToCopy.Count == 0)
            return docxBytes;

        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
                CopyEntry(entry, writeArchive);

            foreach (var mediaPath in mediaToCopy)
            {
                var source = DocxZipHelper.GetRequiredEntry(designArchive, mediaPath);
                CopyEntryToPath(source, writeArchive, mediaPath);
            }

            var ctPath = "[Content_Types].xml";
            var ctXml = ReadEntryText(readArchive, ctPath);
            var ctDoc = XDocument.Parse(ctXml);
            var ctRoot = ctDoc.Root ?? throw new InvalidOperationException("Invalid content types.");
            foreach (var mediaPath in mediaToCopy)
            {
                var extension = Path.GetExtension(mediaPath).TrimStart('.');
                if (string.Equals(extension, "png", StringComparison.OrdinalIgnoreCase))
                    AddDefault(ctRoot, "png", "image/png");
                else if (string.Equals(extension, "jpeg", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(extension, "jpg", StringComparison.OrdinalIgnoreCase))
                    AddDefault(ctRoot, "jpeg", "image/jpeg");

                AddOverride(ctRoot, "/" + mediaPath, ResolveMediaContentType(extension));
            }

            WriteEntry(writeArchive, ctPath, ctDoc.Declaration + ctDoc.ToString(SaveOptions.DisableFormatting));
        }

        return output.ToArray();
    }

    private static bool HeaderXmlHasContent(string headerXml) =>
        Regex.IsMatch(headerXml, "<w:t[^>]*>\\s*\\S", RegexOptions.CultureInvariant)
        || headerXml.Contains("a:blip", StringComparison.OrdinalIgnoreCase)
        || headerXml.Contains(":blip", StringComparison.OrdinalIgnoreCase)
        || headerXml.Contains("wp:docPr", StringComparison.OrdinalIgnoreCase);

    private static PrimaryHeaderSelection? ResolvePrimaryHeader(ZipArchive designArchive)
    {
        var headerParts = designArchive.Entries
            .Select(e => DocxZipHelper.NormalizeEntryPath(e.FullName))
            .Where(p => p.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)
                        && p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (headerParts.Count == 0)
            return null;

        var preferred = TryResolveDefaultHeaderPart(designArchive);
        if (preferred is not null
            && headerParts.Contains(preferred, StringComparer.OrdinalIgnoreCase)
            && HeaderPartHasContent(designArchive, preferred))
        {
            return CreateSelection(preferred);
        }

        var richest = headerParts
            .OrderByDescending(part => MeasureHeaderRichness(designArchive, part))
            .First();

        if (MeasureHeaderRichness(designArchive, richest) == 0)
            return CreateSelection(headerParts[0]);

        return CreateSelection(richest);
    }

    private static PrimaryHeaderSelection CreateSelection(string headerPartPath)
    {
        var relsPath = ResolveHeaderRelsPath(headerPartPath);
        return new PrimaryHeaderSelection(headerPartPath, relsPath);
    }

    private static string? TryResolveDefaultHeaderPart(ZipArchive designArchive)
    {
        if (DocxZipHelper.GetEntry(designArchive, "word/document.xml") is null
            || DocxZipHelper.GetEntry(designArchive, "word/_rels/document.xml.rels") is null)
            return null;

        var documentXml = ReadEntryText(designArchive, "word/document.xml");
        var relsXml = ReadEntryText(designArchive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(documentXml);
        var rels = XDocument.Parse(relsXml);

        var defaultRef = doc.Descendants(W + "headerReference")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute(W + "type"), "default", StringComparison.OrdinalIgnoreCase))
            ?? doc.Descendants(W + "headerReference").FirstOrDefault();

        var relId = (string?)defaultRef?.Attribute(R + "id");
        if (string.IsNullOrWhiteSpace(relId))
            return null;

        var rel = rels.Root?.Elements(RelNs + "Relationship")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("Id"), relId, StringComparison.OrdinalIgnoreCase));

        var target = (string?)rel?.Attribute("Target");
        if (string.IsNullOrWhiteSpace(target))
            return null;

        return DocxZipHelper.NormalizeEntryPath($"word/{target.Replace('\\', '/')}");
    }

    private static List<(string SourcePath, string TargetPath)> CollectDesignParts(
        ZipArchive designArchive,
        PrimaryHeaderSelection selection)
    {
        var parts = new List<(string SourcePath, string TargetPath)>
        {
            (selection.SourceHeaderPartPath, TargetHeaderPartPath)
        };

        if (!string.IsNullOrWhiteSpace(selection.SourceHeaderRelsPath)
            && DocxZipHelper.GetEntry(designArchive, selection.SourceHeaderRelsPath) is not null)
        {
            parts.Add((selection.SourceHeaderRelsPath, TargetHeaderRelsPath));
            var relsText = ReadEntryText(designArchive, selection.SourceHeaderRelsPath);
            foreach (var mediaPath in ExtractRelationshipTargets(relsText, "media/"))
            {
                var normalized = DocxZipHelper.NormalizeEntryPath($"word/{mediaPath}");
                parts.Add((normalized, normalized));
            }
        }

        foreach (var entry in designArchive.Entries)
        {
            var normalized = DocxZipHelper.NormalizeEntryPath(entry.FullName);
            if (!normalized.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase))
                continue;

            parts.Add((normalized, normalized));
        }

        return parts
            .GroupBy(p => p.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static string? ResolveHeaderRelsPath(string headerPartPath)
    {
        var fileName = Path.GetFileName(headerPartPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return $"word/_rels/{fileName}.rels";
    }

    private static int MeasureHeaderTextLength(ZipArchive archive, string headerPartPath)
    {
        var entry = DocxZipHelper.GetEntry(archive, headerPartPath);
        if (entry is null)
            return 0;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xml = reader.ReadToEnd();
        return Regex.Matches(xml, "<w:t[^>]*>([^<]*)</w:t>", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Sum(m => m.Groups[1].Value.Trim().Length);
    }

    private static int MeasureHeaderRichness(ZipArchive archive, string headerPartPath)
    {
        var entry = DocxZipHelper.GetEntry(archive, headerPartPath);
        if (entry is null)
            return 0;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var xml = reader.ReadToEnd();
        var score = MeasureHeaderTextLength(archive, headerPartPath);
        if (HeaderXmlHasContent(xml))
            score += 1000;
        return score;
    }

    private static bool HeaderPartHasContent(ZipArchive archive, string headerPartPath)
    {
        var entry = DocxZipHelper.GetEntry(archive, headerPartPath);
        if (entry is null)
            return false;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return HeaderXmlHasContent(reader.ReadToEnd());
    }

    private static bool IsHeaderEmpty(ZipArchive archive, string headerPartPath) =>
        !HeaderPartHasContent(archive, headerPartPath);

    private static IEnumerable<string> ExtractRelationshipTargets(string relsXml, string targetPrefix)
    {
        var doc = XDocument.Parse(relsXml);
        foreach (var rel in doc.Root?.Elements(RelNs + "Relationship") ?? Enumerable.Empty<XElement>())
        {
            var target = (string?)rel.Attribute("Target");
            if (string.IsNullOrWhiteSpace(target))
                continue;
            if (target.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
                yield return target.Replace('\\', '/');
        }
    }

    private static bool IsReplacedPart(string path)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        return normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(TargetHeaderPartPath, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(TargetHeaderRelsPath, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDocumentRels(ZipArchive targetArchive)
    {
        var existing = ReadEntryText(targetArchive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid document rels.");

        root.Elements(RelNs + "Relationship")
            .Where(r => string.Equals((string?)r.Attribute("Target"), "header1.xml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals((string?)r.Attribute("Id"), LetterheadInjector.HeaderReferenceRelId, StringComparison.OrdinalIgnoreCase))
            .Remove();

        root.Add(new XElement(RelNs + "Relationship",
            new XAttribute("Id", LetterheadInjector.HeaderReferenceRelId),
            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/header"),
            new XAttribute("Target", "header1.xml")));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string InjectHeaderReference(string documentXml) =>
        DocxSectPrHelper.UpsertSectionReferences(
            documentXml,
            includeHeader: true,
            headerRelId: LetterheadInjector.HeaderReferenceRelId,
            includeFooter: false,
            footerRelId: FooterInjector.FooterReferenceRelId);

    private static string BuildContentTypes(
        ZipArchive targetArchive,
        IReadOnlyList<(string SourcePath, string TargetPath)> copiedParts)
    {
        var existing = ReadEntryText(targetArchive, "[Content_Types].xml");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid content types.");

        RemoveOverride(root, "/word/header1.xml");
        AddOverride(root, "/word/header1.xml",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml");

        foreach (var (_, targetPath) in copiedParts)
        {
            var normalized = DocxZipHelper.NormalizeEntryPath(targetPath);
            if (!normalized.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase))
                continue;

            var partName = "/" + normalized;
            RemoveOverride(root, partName);
            var extension = Path.GetExtension(normalized).TrimStart('.');
            if (string.Equals(extension, "png", StringComparison.OrdinalIgnoreCase))
                AddDefault(root, "png", "image/png");
            else if (string.Equals(extension, "jpeg", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(extension, "jpg", StringComparison.OrdinalIgnoreCase))
                AddDefault(root, "jpeg", "image/jpeg");

            AddOverride(root, partName, ResolveMediaContentType(extension));
        }

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string ResolveMediaContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            _ => "application/octet-stream"
        };

    private static void RemoveOverride(XElement root, string partName)
    {
        root.Elements()
            .Where(e => string.Equals((string?)e.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase))
            .Remove();
    }

    private static void AddOverride(XElement root, string partName, string contentType)
    {
        if (root.Elements().Any(e =>
                string.Equals((string?)e.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase)))
            return;

        root.Add(new XElement(root.Name.Namespace + "Override",
            new XAttribute("PartName", partName),
            new XAttribute("ContentType", contentType)));
    }

    private static void AddDefault(XElement root, string extension, string contentType)
    {
        if (root.Elements()
            .Any(e => string.Equals((string?)e.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase)))
            return;

        root.Add(new XElement(root.Name.Namespace + "Default",
            new XAttribute("Extension", extension),
            new XAttribute("ContentType", contentType)));
    }

    private static string ReadEntryText(ZipArchive archive, string path)
    {
        var entry = DocxZipHelper.GetRequiredEntry(archive, path);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CopyEntry(ZipArchiveEntry source, ZipArchive target)
    {
        var entry = target.CreateEntry(source.FullName, CompressionLevel.Optimal);
        using var src = source.Open();
        using var dst = entry.Open();
        src.CopyTo(dst);
    }

    private static void CopyEntryToPath(ZipArchiveEntry source, ZipArchive target, string targetPath)
    {
        var entry = target.CreateEntry(targetPath, CompressionLevel.Optimal);
        using var src = source.Open();
        using var dst = entry.Open();
        src.CopyTo(dst);
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
