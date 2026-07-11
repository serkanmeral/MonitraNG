using System.IO.Compression;
using System.Text;

namespace MngDocument.Infrastructure.Services;

/// <summary>Zimmet teslim / iade tutanakları (parentRow) — DOCX placeholder'lar.</summary>
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
            <w:rPrDefault><w:rPr/></w:rPrDefault>
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

    public static byte[] CreateTeslim() =>
        Create(
            title: "ZİMMET TESLİM TUTANAĞI",
            intro: "Aşağıdaki demirbaş personele teslim edilmiştir.");

    public static byte[] CreateIade() =>
        Create(
            title: "ZİMMET İADE TUTANAĞI",
            intro: "Aşağıdaki demirbaş personelden iade alınmıştır.");

    private static byte[] Create(string title, string intro)
    {
        var lines = new[]
        {
            title,
            "",
            intro,
            "",
            "Demirbaş no: {{demirbasNo}}",
            "Ürün: {{ad}}",
            "Seri no: {{seriNo}}",
            "Durum: {{durum}}",
            "Zimmetli personel: {{zimmetliPersonelId}}",
            "Zimmet WI: {{zimmetRef}}",
            "Garanti bitiş: {{garantiBitis}}",
            "",
            "Üretim: {{generatedAt}}",
            "Rapor: {{reportTitle}}",
            "",
            "Teslim / iade eden imza: ________________",
            "Personel imza: ________________"
        };

        var paragraphs = string.Join(
            "",
            lines.Select(line =>
                $"<w:p><w:r><w:t xml:space=\"preserve\">{EscapeXml(line)}</w:t></w:r></w:p>"));

        var docXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                {paragraphs}
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
