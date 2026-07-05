using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

public sealed class LetterheadApplyRequest
{
    public required TemplateLetterheadModel Letterhead { get; init; }
    public required string DocumentName { get; init; }
    public byte[]? LogoBytes { get; init; }
    public string LogoExtension { get; init; } = ".png";
}

/// <summary>Applies or refreshes a 3-column DOCX header (logo | name | placeholders).</summary>
public static class LetterheadInjector
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace A =
        "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Pic =
        "http://schemas.openxmlformats.org/drawingml/2006/picture";
    private static readonly XNamespace Wp =
        "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace RelNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string HeaderPartPath = "word/header1.xml";
    private const string HeaderRelsPath = "word/_rels/header1.xml.rels";
    private const string MediaLogoPath = "word/media/letterhead-logo.png";
    private const string HeaderRelId = "rId1";
    internal const string HeaderReferenceRelId = "rIdHeader1";

    public static byte[] Apply(byte[] docxBytes, LetterheadApplyRequest request)
    {
        if (!request.Letterhead.Enabled)
            return docxBytes;

        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = ReadEntryText(readArchive, "word/document.xml");
            var hasLogo = request.Letterhead.ShowLogo && request.LogoBytes is { Length: > 0 };

            foreach (var entry in readArchive.Entries)
            {
                if (IsReplacedPart(entry.FullName))
                    continue;
                CopyEntry(entry, writeArchive);
            }

            WriteEntry(writeArchive, HeaderPartPath, BuildHeaderXml(request, hasLogo));
            WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildDocumentRels(readArchive, hasLogo));
            WriteEntry(writeArchive, "word/document.xml", InjectHeaderReference(documentXml));
            WriteEntry(writeArchive, "[Content_Types].xml", BuildContentTypes(readArchive, hasLogo));

            if (hasLogo)
            {
                WriteEntryBytes(writeArchive, MediaLogoPath, request.LogoBytes!);
                WriteEntry(writeArchive, HeaderRelsPath, BuildHeaderRels(hasLogo));
            }
        }

        return output.ToArray();
    }

    public static byte[] RemoveHeader(byte[] docxBytes) => docxBytes;

    private static bool IsReplacedPart(string path)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        return normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(HeaderPartPath, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(HeaderRelsPath, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(MediaLogoPath, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/media/image1.jpeg", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/media/image1.jpg", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildHeaderXml(LetterheadApplyRequest request, bool hasLogo)
    {
        var lh = request.Letterhead;
        var left = hasLogo ? BuildLogoParagraph() : BuildTextParagraph(string.Empty);
        var center = lh.ShowDocumentName
            ? BuildTextParagraph(LetterheadConstants.DocumentNameToken, bold: true, align: "center")
            : BuildTextParagraph(string.Empty);
        var rightLines = new List<string>();
        if (lh.ShowDocumentNumber)
            rightLines.Add(LetterheadConstants.DocNoToken);
        if (lh.ShowGeneratedAt)
            rightLines.Add(LetterheadConstants.GeneratedAtToken);
        if (lh.ShowCreatePerson)
            rightLines.Add(LetterheadConstants.CreatePersonToken);
        var right = rightLines.Count > 0
            ? BuildTextParagraph(EscapeXml(string.Join("\n", rightLines)), align: "right")
            : BuildTextParagraph(string.Empty);

        return $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <w:hdr xmlns:w="{W}" xmlns:r="{R}" xmlns:wp="{Wp}" xmlns:a="{A}" xmlns:pic="{Pic}">
                  <w:tbl>
                    <w:tblPr>
                      <w:tblW w:w="5000" w:type="pct"/>
                      <w:tblBorders>
                        <w:bottom w:val="single" w:sz="4" w:space="0" w:color="auto"/>
                      </w:tblBorders>
                    </w:tblPr>
                    <w:tr>
                      <w:tc><w:tcPr><w:vAlign w:val="center"/></w:tcPr>{left}</w:tc>
                      <w:tc><w:tcPr><w:vAlign w:val="center"/></w:tcPr>{center}</w:tc>
                      <w:tc><w:tcPr><w:vAlign w:val="center"/></w:tcPr>{right}</w:tc>
                    </w:tr>
                  </w:tbl>
                </w:hdr>
                """;
    }

    private static string BuildLogoParagraph() =>
        """
        <w:p>
          <w:pPr><w:jc w:val="left"/></w:pPr>
          <w:r>
            <w:drawing>
              <wp:inline distT="0" distB="0" distL="0" distR="0">
                <wp:extent cx="1143000" cy="571500"/>
                <wp:docPr id="1" name="LetterheadLogo"/>
                <a:graphic>
                  <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                    <pic:pic>
                      <pic:nvPicPr>
                        <pic:cNvPr id="0" name="LetterheadLogo"/>
                        <pic:cNvPicPr/>
                      </pic:nvPicPr>
                      <pic:blipFill>
                        <a:blip r:embed="rId1"/>
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
        """;

    private static string BuildTextParagraph(string text, bool bold = false, string align = "left")
    {
        var runProps = bold ? "<w:rPr><w:b/></w:rPr>" : string.Empty;
        var lines = text.Split('\n');
        if (lines.Length <= 1)
        {
            return $"""
                    <w:p>
                      <w:pPr><w:jc w:val="{align}"/></w:pPr>
                      <w:r>{runProps}<w:t xml:space="preserve">{text}</w:t></w:r>
                    </w:p>
                    """;
        }

        var sb = new StringBuilder();
        sb.Append($"<w:p><w:pPr><w:jc w:val=\"{align}\"/></w:pPr>");
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                sb.Append("<w:r><w:br/></w:r>");
            sb.Append($"<w:r>{runProps}<w:t xml:space=\"preserve\">{lines[i]}</w:t></w:r>");
        }

        sb.Append("</w:p>");
        return sb.ToString();
    }

    private static string BuildHeaderRels(bool hasLogo)
    {
        if (!hasLogo)
            return EmptyRelsXml();

        return $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="{RelNs}">
                  <Relationship Id="{HeaderRelId}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/letterhead-logo.png"/>
                </Relationships>
                """;
    }

    private static string BuildDocumentRels(ZipArchive archive, bool hasLogo)
    {
        var existing = ReadEntryText(archive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid document rels.");

        root.Elements(RelNs + "Relationship")
            .Where(r => string.Equals((string?)r.Attribute("Target"), "header1.xml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals((string?)r.Attribute("Id"), HeaderReferenceRelId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals((string?)r.Attribute("Id"), "rId8", StringComparison.OrdinalIgnoreCase))
            .Remove();

        if (hasLogo || true)
        {
            root.Add(new XElement(RelNs + "Relationship",
                new XAttribute("Id", HeaderReferenceRelId),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/header"),
                new XAttribute("Target", "header1.xml")));
        }

        foreach (var rel in root.Elements(RelNs + "Relationship"))
        {
            if (string.Equals((string?)rel.Attribute("Target"), "styles.xml", StringComparison.OrdinalIgnoreCase))
                continue;
        }

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string InjectHeaderReference(string documentXml) =>
        DocxSectPrHelper.UpsertSectionReferences(
            documentXml,
            includeHeader: true,
            headerRelId: HeaderReferenceRelId,
            includeFooter: false,
            footerRelId: FooterInjector.FooterReferenceRelId);

    private static string BuildContentTypes(ZipArchive archive, bool hasLogo)
    {
        var existing = ReadEntryText(archive, "[Content_Types].xml");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid content types.");

        RemoveOverride(root, "/word/header1.xml");
        RemoveOverride(root, "/word/media/letterhead-logo.png");

        AddOverride(root, "/word/header1.xml",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml");

        if (hasLogo)
        {
            AddDefault(root, "png", "image/png");
            AddOverride(root, "/word/media/letterhead-logo.png", "image/png");
        }

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

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

    private static string EmptyRelsXml() =>
        $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="{RelNs}"/>""";

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

    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
