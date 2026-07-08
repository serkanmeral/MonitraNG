using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>Inserts or refreshes a letterhead-style logo band at the top of a cover page DOCX body.</summary>
public static class CoverPageLogoInjector
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace RelNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    internal const string ImageRelId = "rIdCoverLogo";
    internal const string LogoDocPrName = "CoverPageLogo";
    internal const string HeaderBandMarker = "CoverPageHeaderBand";

    /// <summary>Initial skeleton / legacy bootstrap — inserts default logo band.</summary>
    public static byte[] Apply(byte[] docxBytes, byte[] logoBytes, string extension) =>
        EnsureLogoForUse(docxBytes, logoBytes, extension, bootstrapIfMissing: true);

    /// <summary>
    /// Keeps saved cover layout intact. Refreshes logo media when present; optionally bootstraps missing logo band.
    /// </summary>
    public static byte[] EnsureLogoForUse(
        byte[] docxBytes,
        byte[] logoBytes,
        string extension,
        bool bootstrapIfMissing = true)
    {
        if (logoBytes is not { Length: > 0 })
            return docxBytes;

        if (TryResolveLogoMediaPath(docxBytes, out var existingMediaPath))
            return RefreshLogoMedia(docxBytes, logoBytes, extension, existingMediaPath);

        if (HasLogoStructure(docxBytes))
            return RefreshLogoMedia(docxBytes, logoBytes, extension, $"word/media/cover-logo{NormalizeExtension(extension)}");

        return bootstrapIfMissing
            ? InjectFreshLogoBand(docxBytes, logoBytes, extension)
            : docxBytes;
    }

    /// <summary>Uses saved cover design as-is; only refreshes domain logo bytes when design already contains a logo.</summary>
    public static byte[] RefreshLogoMediaIfPresent(byte[] docxBytes, byte[] logoBytes, string extension) =>
        EnsureLogoForUse(docxBytes, logoBytes, extension, bootstrapIfMissing: false);

    public static bool HasLogoStructure(byte[] docxBytes)
    {
        try
        {
            using var input = new MemoryStream(docxBytes, writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var documentXml = ReadEntryText(archive, "word/document.xml");
            if (documentXml.Contains(LogoDocPrName, StringComparison.Ordinal)
                || documentXml.Contains(HeaderBandMarker, StringComparison.Ordinal)
                || documentXml.Contains(ImageRelId, StringComparison.Ordinal))
            {
                return true;
            }

            var rels = ReadEntryText(archive, "word/_rels/document.xml.rels");
            return rels.Contains("media/cover-logo", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveLogoMediaPath(byte[] docxBytes, out string mediaPath)
    {
        mediaPath = string.Empty;
        try
        {
            using var input = new MemoryStream(docxBytes, writable: false);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("word/media/cover-logo", StringComparison.OrdinalIgnoreCase))
                {
                    mediaPath = DocxZipHelper.NormalizeEntryPath(entry.FullName);
                    return true;
                }
            }

            var documentXml = ReadEntryText(archive, "word/document.xml");
            var relsXml = ReadEntryText(archive, "word/_rels/document.xml.rels");
            var relsDoc = XDocument.Parse(relsXml);
            var relById = relsDoc.Root?
                .Elements(RelNs + "Relationship")
                .Where(r => !string.IsNullOrWhiteSpace((string?)r.Attribute("Id")))
                .ToDictionary(
                    r => (string)r.Attribute("Id")!,
                    r => NormalizeRelTarget((string?)r.Attribute("Target")),
                    StringComparer.Ordinal);

            if (relById is null || relById.Count == 0)
                return false;

            var doc = XDocument.Parse(documentXml);
            foreach (var blip in doc.Descendants().Where(e => e.Name.LocalName == "blip"))
            {
                var embedAttr = blip.Attributes()
                    .FirstOrDefault(a => a.Name.LocalName == "embed");
                if (embedAttr is null || !relById.TryGetValue(embedAttr.Value, out var target))
                    continue;

                if (!target.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
                    continue;

                mediaPath = $"word/{target.TrimStart('/')}";
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static byte[] RefreshLogoMedia(
        byte[] docxBytes,
        byte[] logoBytes,
        string extension,
        string mediaPath)
    {
        var ext = NormalizeExtension(extension);
        var normalizedMediaPath = DocxZipHelper.NormalizeEntryPath(mediaPath);
        var useCoverLogoPath = normalizedMediaPath.Contains("cover-logo", StringComparison.OrdinalIgnoreCase);
        var targetMediaPath = useCoverLogoPath ? $"word/media/cover-logo{ext}" : normalizedMediaPath;

        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
            {
                if (IsReplacedPart(entry.FullName, ext, targetMediaPath, useCoverLogoPath))
                    continue;
                CopyEntry(entry, writeArchive);
            }

            if (useCoverLogoPath)
            {
                WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildDocumentRels(readArchive, ext));
                WriteEntry(writeArchive, "[Content_Types].xml", BuildContentTypes(readArchive, ext));
            }

            WriteEntryBytes(writeArchive, targetMediaPath, logoBytes);
        }

        return output.ToArray();
    }

    private static byte[] InjectFreshLogoBand(byte[] docxBytes, byte[] logoBytes, string extension)
    {
        var ext = NormalizeExtension(extension);
        var mediaPath = $"word/media/cover-logo{ext}";

        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = ReadEntryText(readArchive, "word/document.xml");

            foreach (var entry in readArchive.Entries)
            {
                var normalized = DocxZipHelper.NormalizeEntryPath(entry.FullName);
                if (normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
                    || IsReplacedPart(entry.FullName, ext, mediaPath, useCoverLogoPath: true))
                {
                    continue;
                }

                CopyEntry(entry, writeArchive);
            }

            var updatedDocumentXml = InjectHeaderBand(documentXml);
            WriteEntry(writeArchive, "word/document.xml", updatedDocumentXml);
            WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildDocumentRels(readArchive, ext));
            WriteEntry(writeArchive, "[Content_Types].xml", BuildContentTypes(readArchive, ext));
            WriteEntryBytes(writeArchive, mediaPath, logoBytes);
        }

        return output.ToArray();
    }

    private static bool IsReplacedPart(string path, string ext, string targetMediaPath, bool useCoverLogoPath)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        if (useCoverLogoPath)
        {
            return normalized.Equals("word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase)
                   || normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
                   || normalized.StartsWith("word/media/cover-logo", StringComparison.OrdinalIgnoreCase);
        }

        return normalized.Equals(targetMediaPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string InjectHeaderBand(string documentXml)
    {
        var doc = XDocument.Parse(documentXml);
        var body = doc.Root?.Element(W + "body")
                   ?? throw new InvalidOperationException("Invalid cover document.xml");

        RemoveLegacyLogoContent(body);

        var headerBand = XElement.Parse(BuildHeaderBandXml());
        var firstContent = body.Elements().FirstOrDefault(e => e.Name != W + "sectPr");
        if (firstContent is null)
            body.Add(headerBand);
        else
            firstContent.AddBeforeSelf(headerBand);

        return SerializeDocument(doc);
    }

    private static void RemoveLegacyLogoContent(XElement body)
    {
        var legacyTables = body.Elements(W + "tbl")
            .Where(t => t.Descendants().Any(e =>
                e.Name.LocalName == "docPr"
                && (string.Equals((string?)e.Attribute("name"), LogoDocPrName, StringComparison.Ordinal)
                    || string.Equals((string?)e.Attribute("name"), HeaderBandMarker, StringComparison.Ordinal))))
            .ToList();
        foreach (var table in legacyTables)
            table.Remove();

        var legacyParagraphs = body.Elements(W + "p")
            .Where(p => p.Descendants().Any(e =>
                e.Name.LocalName == "docPr"
                && string.Equals((string?)e.Attribute("name"), LogoDocPrName, StringComparison.Ordinal)))
            .ToList();
        foreach (var paragraph in legacyParagraphs)
            paragraph.Remove();
    }

    private static string BuildHeaderBandXml() =>
        $"""
         <w:tbl xmlns:w="{W}" xmlns:r="{R}" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
           <w:tblPr>
             <w:tblW w:w="5000" w:type="pct"/>
             <w:tblBorders>
               <w:top w:val="none" w:sz="0" w:space="0" w:color="auto"/>
               <w:left w:val="none" w:sz="0" w:space="0" w:color="auto"/>
               <w:right w:val="none" w:sz="0" w:space="0" w:color="auto"/>
               <w:insideH w:val="none" w:sz="0" w:space="0" w:color="auto"/>
               <w:insideV w:val="none" w:sz="0" w:space="0" w:color="auto"/>
               <w:bottom w:val="single" w:sz="4" w:space="0" w:color="auto"/>
             </w:tblBorders>
             <w:tblLook w:val="0000" w:firstRow="0" w:lastRow="0" w:firstColumn="0" w:lastColumn="0" w:noHBand="1" w:noVBand="1"/>
           </w:tblPr>
           <w:tr>
             <w:trPr><w:trHeight w:val="900" w:hRule="atLeast"/></w:trPr>
             <w:tc>
               <w:tcPr><w:vAlign w:val="center"/></w:tcPr>
               <w:p>
                 <w:pPr><w:jc w:val="left"/><w:spacing w:after="120"/></w:pPr>
                 <w:r>
                   <w:drawing>
                     <wp:inline distT="0" distB="0" distL="0" distR="0">
                       <wp:extent cx="1143000" cy="571500"/>
                       <wp:docPr id="2" name="{HeaderBandMarker}"/>
                       <a:graphic>
                         <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                           <pic:pic>
                             <pic:nvPicPr>
                               <pic:cNvPr id="0" name="{LogoDocPrName}"/>
                               <pic:cNvPicPr/>
                             </pic:nvPicPr>
                             <pic:blipFill>
                               <a:blip r:embed="{ImageRelId}"/>
                               <a:stretch><a:fillRect/></a:stretch>
                             </pic:blipFill>
                             <pic:spPr>
                               <a:xfrm><a:off x="0" y="0"/><a:ext cx="1143000" cy="571500"/></a:xfrm>
                               <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                             </pic:spPr>
                           </pic:pic>
                         </a:graphicData>
                       </a:graphic>
                     </wp:inline>
                   </w:drawing>
                 </w:r>
               </w:p>
             </w:tc>
           </w:tr>
         </w:tbl>
         """;

    private static string BuildDocumentRels(ZipArchive archive, string ext)
    {
        var existing = ReadEntryText(archive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid document rels.");

        root.Elements(RelNs + "Relationship")
            .Where(r =>
            {
                var target = (string?)r.Attribute("Target") ?? string.Empty;
                var id = (string?)r.Attribute("Id") ?? string.Empty;
                return target.Contains("media/cover-logo", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(id, ImageRelId, StringComparison.OrdinalIgnoreCase);
            })
            .Remove();

        root.Add(new XElement(RelNs + "Relationship",
            new XAttribute("Id", ImageRelId),
            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"),
            new XAttribute("Target", $"media/cover-logo{ext}")));

        return SerializeDocument(doc);
    }

    private static string BuildContentTypes(ZipArchive archive, string ext)
    {
        var existing = ReadEntryText(archive, "[Content_Types].xml");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid content types.");

        var partName = $"/word/media/cover-logo{ext}";
        root.Elements()
            .Where(e => string.Equals((string?)e.Attribute("PartName"), partName, StringComparison.OrdinalIgnoreCase)
                        || ((string?)e.Attribute("PartName") ?? string.Empty).StartsWith("/word/media/cover-logo", StringComparison.OrdinalIgnoreCase))
            .Remove();

        var extension = ext.TrimStart('.');
        var contentType = extension.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                          || extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
            ? "image/jpeg"
            : "image/png";

        if (!root.Elements().Any(e =>
                string.Equals((string?)e.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase)))
        {
            root.Add(new XElement(root.Name.Namespace + "Default",
                new XAttribute("Extension", extension),
                new XAttribute("ContentType", contentType)));
        }

        root.Add(new XElement(root.Name.Namespace + "Override",
            new XAttribute("PartName", partName),
            new XAttribute("ContentType", contentType)));

        return SerializeDocument(doc);
    }

    private static string NormalizeRelTarget(string? target) =>
        (target ?? string.Empty).Replace('\\', '/').Trim();

    private static string SerializeDocument(XDocument doc)
    {
        if (doc.Declaration is not null)
            return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string NormalizeExtension(string? extension)
    {
        var ext = (extension ?? ".png").Trim().ToLowerInvariant();
        if (!ext.StartsWith('.'))
            ext = "." + ext;
        return ext switch
        {
            ".jpg" or ".jpeg" => ".jpeg",
            ".png" => ".png",
            _ => ".png"
        };
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

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteEntryBytes(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}
