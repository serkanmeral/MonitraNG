using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using MngDocument.Application.Configuration;

namespace MngDocument.Infrastructure.Services;

public sealed class FooterApplyRequest
{
    public required TemplateFooterModel Footer { get; init; }
    public required DomainFooterProfileSettings Profile { get; init; }
    public TemplatePageLayoutModel? PageLayout { get; init; }
}

/// <summary>Applies Odak-style dual-office corporate footer (F86 revision line + addresses + contacts).</summary>
public static class FooterInjector
{
    private static readonly XNamespace W = DocxSectPrHelper.W;
    private static readonly XNamespace R = DocxSectPrHelper.R;
    private static readonly XNamespace RelNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string FooterPartPath = "word/footer1.xml";
    internal const string FooterReferenceRelId = "rIdFooter1";

    // ODK-COC-23-202.docx printable width (pgSz.w - pgMar.left - pgMar.right)
    private const int ContentWidthTwips = 8316;
    private const int ColumnWidthTwips = ContentWidthTwips / 2;

    public static byte[] Apply(byte[] docxBytes, FooterApplyRequest request)
    {
        if (!request.Footer.Enabled)
            return docxBytes;

        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = ReadEntryText(readArchive, "word/document.xml");

            foreach (var entry in readArchive.Entries)
            {
                if (IsReplacedPart(entry.FullName))
                    continue;
                CopyEntry(entry, writeArchive);
            }

            WriteEntry(writeArchive, FooterPartPath, BuildFooterXml(request));
            WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildDocumentRels(readArchive));
            WriteEntry(writeArchive, "word/document.xml", InjectFooterReference(documentXml));
            WriteEntry(writeArchive, "[Content_Types].xml", BuildContentTypes(readArchive));
        }

        return output.ToArray();
    }

    private static bool IsReplacedPart(string path)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        return normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(FooterPartPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFooterXml(FooterApplyRequest request)
    {
        var footer = request.Footer;
        var profile = request.Profile;
        var layout = request.PageLayout ?? TemplatePageLayoutModel.CreateDefault();
        var offices = profile.Offices.Take(2).ToList();
        while (offices.Count < 2)
            offices.Add(new DomainOfficeSettings());

        var rows = new List<string>();

        if (footer.ShowFormRevision)
        {
            var revision = $"{EscapeXml(profile.FormCode)} {EscapeXml(profile.FormRevision)} {EscapeXml(profile.FormRevisionDate)}".Trim();
            rows.Add(BuildMergedRow(revision, RevisionRunProps));
        }

        if (footer.ShowOfficeColumns)
        {
            rows.Add(BuildTwoColumnRow(
                EscapeXml(offices[0].Label),
                EscapeXml(offices[1].Label),
                bold: true,
                leftIndentTwips: 0));
        }

        if (footer.ShowAddresses)
        {
            rows.Add(BuildTwoColumnRow(
                EscapeXml(offices[0].Address),
                EscapeXml(offices[1].Address),
                bold: false,
                leftIndentTwips: 0));
        }

        if (footer.ShowDividerLine)
            rows.Add(BuildDividerRow(leftIndentTwips: 0));

        if (footer.ShowContacts)
        {
            rows.Add(BuildTwoColumnRow(
                BuildContactLine(offices[0]),
                BuildContactLine(offices[1]),
                bold: false,
                leftIndentTwips: 0));
        }

        rows.Add(BuildMergedRow(string.Empty, SpacerRunProps));

        return $"""
                  <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                  <w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                    {BuildFooterTable(rows, layout.FooterLeftIndentTwips)}
                  </w:ftr>
                  """;
    }

    private static string BuildFooterTable(IEnumerable<string> rows, int leftIndentTwips) =>
        $"""
         <w:tbl>
           <w:tblPr>
             <w:tblW w:w="5000" w:type="pct"/>
             <w:tblInd w:w="{leftIndentTwips}" w:type="dxa"/>
             <w:tblLayout w:type="fixed"/>
             <w:tblCellMar>
               <w:top w:w="0" w:type="dxa"/>
               <w:left w:w="0" w:type="dxa"/>
               <w:bottom w:w="0" w:type="dxa"/>
               <w:right w:w="0" w:type="dxa"/>
             </w:tblCellMar>
             <w:tblLook w:val="04A0" w:firstRow="1" w:lastRow="0" w:firstColumn="1" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/>
           </w:tblPr>
           <w:tblGrid>
             <w:gridCol w:w="{ColumnWidthTwips}"/>
             <w:gridCol w:w="{ColumnWidthTwips}"/>
           </w:tblGrid>
           {string.Concat(rows)}
         </w:tbl>
         """;

    private static string BuildTwoColumnRow(string left, string right, bool bold, int leftIndentTwips)
    {
        var runProps = bold ? BoldRunProps : NormalRunProps;
        return $"""
                  <w:tr>
                    {BuildTableCell(left, runProps, ColumnWidthTwips)}
                    {BuildTableCell(right, runProps, ColumnWidthTwips)}
                  </w:tr>
                  """;
    }

    private static string BuildMergedRow(string text, string runProps) =>
        $"""
         <w:tr>
           <w:tc>
             <w:tcPr>
               <w:gridSpan w:val="2"/>
               <w:tcW w:w="{ContentWidthTwips}" w:type="dxa"/>
             </w:tcPr>
             <w:p>
               <w:pPr><w:jc w:val="both"/></w:pPr>
               <w:r>{runProps}<w:t xml:space="preserve">{text}</w:t></w:r>
             </w:p>
           </w:tc>
         </w:tr>
         """;

    private static string BuildDividerRow(int leftIndentTwips) =>
        $"""
         <w:tr>
           <w:tc>
             <w:tcPr>
               <w:gridSpan w:val="2"/>
               <w:tcW w:w="{ContentWidthTwips}" w:type="dxa"/>
             </w:tcPr>
             <w:p>
               <w:pPr>
                 <w:pBdr>
                   <w:top w:val="single" w:sz="12" w:space="1" w:color="231F20"/>
                 </w:pBdr>
               </w:pPr>
             </w:p>
           </w:tc>
         </w:tr>
         """;

    private static string BuildTableCell(string text, string runProps, int widthTwips) =>
        $"""
         <w:tc>
           <w:tcPr>
             <w:tcW w:w="{widthTwips}" w:type="dxa"/>
             <w:vAlign w:val="top"/>
           </w:tcPr>
           <w:p>
             <w:pPr><w:jc w:val="both"/></w:pPr>
             <w:r>{runProps}<w:t xml:space="preserve">{text}</w:t></w:r>
           </w:p>
         </w:tc>
         """;

    private const string RevisionRunProps =
        "<w:rPr><w:sz w:val=\"14\"/><w:szCs w:val=\"12\"/></w:rPr>";

    private const string SpacerRunProps =
        "<w:rPr><w:sz w:val=\"16\"/><w:szCs w:val=\"16\"/></w:rPr>";

    private const string BoldRunProps =
        "<w:rPr><w:rFonts w:ascii=\"Tahoma\" w:hAnsi=\"Tahoma\" w:cs=\"Tahoma\"/><w:b/><w:color w:val=\"231F20\"/><w:w w:val=\"80\"/><w:sz w:val=\"16\"/><w:szCs w:val=\"16\"/></w:rPr>";

    private const string NormalRunProps =
        "<w:rPr><w:rFonts w:ascii=\"Tahoma\" w:hAnsi=\"Tahoma\" w:cs=\"Tahoma\"/><w:color w:val=\"231F20\"/><w:w w:val=\"80\"/><w:kern w:val=\"22\"/><w:sz w:val=\"16\"/><w:szCs w:val=\"16\"/></w:rPr>";

    private static string BuildContactLine(DomainOfficeSettings office)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(office.Phone))
            parts.Add($"Tel: {office.Phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(office.Fax))
            parts.Add($"Faks: {office.Fax.Trim()}");
        return EscapeXml(string.Join("     ", parts));
    }

    private static string BuildDocumentRels(ZipArchive archive)
    {
        var existing = ReadEntryText(archive, "word/_rels/document.xml.rels");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid document rels.");

        root.Elements(RelNs + "Relationship")
            .Where(r => string.Equals((string?)r.Attribute("Target"), "footer1.xml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals((string?)r.Attribute("Id"), FooterReferenceRelId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals((string?)r.Attribute("Id"), "rId9", StringComparison.OrdinalIgnoreCase))
            .Remove();

        root.Add(new XElement(RelNs + "Relationship",
            new XAttribute("Id", FooterReferenceRelId),
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
            footerRelId: FooterReferenceRelId);

    private static string BuildContentTypes(ZipArchive archive)
    {
        var existing = ReadEntryText(archive, "[Content_Types].xml");
        var doc = XDocument.Parse(existing);
        var root = doc.Root ?? throw new InvalidOperationException("Invalid content types.");

        root.Elements()
            .Where(e => string.Equals((string?)e.Attribute("PartName"), "/word/footer1.xml", StringComparison.OrdinalIgnoreCase))
            .Remove();

        root.Add(new XElement(root.Name.Namespace + "Override",
            new XAttribute("PartName", "/word/footer1.xml"),
            new XAttribute("ContentType",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml")));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
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

    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
