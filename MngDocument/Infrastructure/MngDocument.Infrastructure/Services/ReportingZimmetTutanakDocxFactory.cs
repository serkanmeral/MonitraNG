using System.IO.Compression;
using System.Text;

namespace MngDocument.Infrastructure.Services;

/// <summary>Zimmet teslim / iade tutanakları — tek sayfalık DOCX.</summary>
public static class ReportingZimmetTutanakDocxFactory
{
    private const string ContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
          <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
          <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
        </Types>
        """;

    private const string RelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>
        </Relationships>
        """;

    private const string StylesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:docDefaults>
            <w:rPrDefault>
              <w:rPr>
                <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>
                <w:sz w:val="22"/>
                <w:szCs w:val="22"/>
                <w:color w:val="1F2937"/>
              </w:rPr>
            </w:rPrDefault>
            <w:pPrDefault><w:pPr/></w:pPrDefault>
          </w:docDefaults>
        </w:styles>
        """;

    private const string SettingsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
          <w:zoom w:percent="100"/>
        </w:settings>
        """;

    private const string Ink = "1F2937";
    private const string Muted = "4B5563";
    private const string Faint = "6B7280";
    private const string Line = "D1D5DB";
    private const string Wash = "F3F4F6";

    public static byte[] CreateTeslim() =>
        Create(
            title: "Zimmet Teslim Tutanağı",
            intro: "Aşağıda bilgileri yer alan demirbaş, zimmetli personele teslim edilmiştir.",
            leftSign: "Teslim eden",
            rightSign: "Teslim alan");

    public static byte[] CreateIade() =>
        Create(
            title: "Zimmet İade Tutanağı",
            intro: "Aşağıda bilgileri yer alan demirbaş, zimmetli personelden iade alınmıştır.",
            leftSign: "İade alan",
            rightSign: "İade eden");

    private static byte[] Create(string title, string intro, string leftSign, string rightSign)
    {
        var rows = new (string Label, string Token)[]
        {
            ("Demirbaş no", "{{demirbasNo}}"),
            ("Ürün", "{{ad}}"),
            ("Seri no", "{{seriNo}}"),
            ("Durum", "{{durum}}"),
            ("Personel", "{{zimmetliPersonelId}}"),
            ("İş kaydı", "{{zimmetRef}}"),
            ("Garanti bitişi", "{{garantiBitis}}"),
        };

        var body = new StringBuilder();
        body.Append(Paragraph(title, 40, Ink, bold: true, align: "center", before: 0, after: 80));
        body.Append(Paragraph(intro, 22, Muted, bold: false, align: "center", before: 0, after: 280));
        body.Append(FieldTable(rows));
        body.Append(Paragraph("", 20, Ink, bold: false, align: "left", before: 200, after: 0));
        body.Append(SignatureTable(leftSign, rightSign));
        body.Append(Paragraph("Üretim: {{generatedAt}}", 16, Faint, bold: false, align: "right", before: 360, after: 0));

        var docXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                {body}
                <w:sectPr>
                  <w:pgSz w:w="11906" w:h="16838"/>
                  <w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="708" w:footer="708" w:gutter="0"/>
                </w:sectPr>
              </w:body>
            </w:document>
            """;

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RelsXml);
            WriteEntry(archive, "word/document.xml", docXml);
            WriteEntry(archive, "word/_rels/document.xml.rels", DocRelsXml);
            WriteEntry(archive, "word/styles.xml", StylesXml);
            WriteEntry(archive, "word/settings.xml", SettingsXml);
        }

        return ms.ToArray();
    }

    private static string FieldTable((string Label, string Token)[] rows)
    {
        var rendered = string.Join("", rows.Select(row =>
            $"""
            <w:tr>
              <w:tc>
                <w:tcPr>
                  <w:tcW w:w="2800" w:type="dxa"/>
                  <w:shd w:val="clear" w:color="auto" w:fill="{Wash}"/>
                  <w:vAlign w:val="center"/>
                </w:tcPr>
                {Paragraph(row.Label, 20, Muted, bold: true, align: "left", before: 40, after: 40)}
              </w:tc>
              <w:tc>
                <w:tcPr>
                  <w:tcW w:w="6838" w:type="dxa"/>
                  <w:vAlign w:val="center"/>
                </w:tcPr>
                {Paragraph(row.Token, 22, Ink, bold: false, align: "left", before: 40, after: 40)}
              </w:tc>
            </w:tr>
            """));

        return Table("9638", "2800", "6838", rendered);
    }

    private static string SignatureTable(string left, string right)
    {
        var cell = (string caption) =>
            $"""
            <w:tc>
              <w:tcPr>
                <w:tcW w:w="4819" w:type="dxa"/>
                <w:tcMar>
                  <w:top w:w="120" w:type="dxa"/>
                  <w:left w:w="140" w:type="dxa"/>
                  <w:bottom w:w="120" w:type="dxa"/>
                  <w:right w:w="140" w:type="dxa"/>
                </w:tcMar>
              </w:tcPr>
              {Paragraph(caption, 22, Ink, bold: true, align: "left", before: 0, after: 200)}
              {LineParagraph("Ad soyad")}
              {LineParagraph("Tarih")}
            </w:tc>
            """;

        var rows =
            $"""
            <w:tr>
              <w:trPr><w:trHeight w:val="1600" w:hRule="atLeast"/></w:trPr>
              {cell(left)}
              {cell(right)}
            </w:tr>
            """;

        return Table("9638", "4819", "4819", rows);
    }

    private static string Table(string width, string left, string right, string rows) =>
        $"""
        <w:tbl>
          <w:tblPr>
            <w:tblW w:w="{width}" w:type="dxa"/>
            <w:tblBorders>
              <w:top w:val="single" w:sz="4" w:space="0" w:color="{Line}"/>
              <w:left w:val="single" w:sz="4" w:space="0" w:color="{Line}"/>
              <w:bottom w:val="single" w:sz="4" w:space="0" w:color="{Line}"/>
              <w:right w:val="single" w:sz="4" w:space="0" w:color="{Line}"/>
              <w:insideH w:val="single" w:sz="4" w:space="0" w:color="{Line}"/>
              <w:insideV w:val="single" w:sz="4" w:space="0" w:color="{Line}"/>
            </w:tblBorders>
            <w:tblCellMar>
              <w:top w:w="60" w:type="dxa"/>
              <w:left w:w="120" w:type="dxa"/>
              <w:bottom w:w="60" w:type="dxa"/>
              <w:right w:w="120" w:type="dxa"/>
            </w:tblCellMar>
          </w:tblPr>
          <w:tblGrid>
            <w:gridCol w:w="{left}"/>
            <w:gridCol w:w="{right}"/>
          </w:tblGrid>
          {rows}
        </w:tbl>
        """;

    private static string Paragraph(
        string text,
        int halfPoints,
        string color,
        bool bold,
        string align,
        int before,
        int after)
    {
        var boldXml = bold ? "<w:b/><w:bCs/>" : "";
        return
            $"""
            <w:p>
              <w:pPr>
                <w:spacing w:before="{before}" w:after="{after}"/>
                <w:jc w:val="{align}"/>
              </w:pPr>
              <w:r>
                <w:rPr>
                  <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>
                  {boldXml}
                  <w:color w:val="{color}"/>
                  <w:sz w:val="{halfPoints}"/>
                  <w:szCs w:val="{halfPoints}"/>
                </w:rPr>
                <w:t xml:space="preserve">{EscapeXml(text)}</w:t>
              </w:r>
            </w:p>
            """;
    }

    private static string LineParagraph(string caption) =>
        $"""
        <w:p>
          <w:pPr>
            <w:spacing w:before="160" w:after="40"/>
            <w:pBdr>
              <w:bottom w:val="single" w:sz="6" w:space="1" w:color="{Line}"/>
            </w:pBdr>
          </w:pPr>
          <w:r>
            <w:rPr>
              <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>
              <w:color w:val="{Faint}"/>
              <w:sz w:val="18"/>
              <w:szCs w:val="18"/>
            </w:rPr>
            <w:t>{EscapeXml(caption)}</w:t>
          </w:r>
        </w:p>
        """;

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
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
