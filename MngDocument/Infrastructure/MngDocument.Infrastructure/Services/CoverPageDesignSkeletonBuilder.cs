using System.Globalization;
using System.IO.Compression;
using System.Text;
using MngDocument.Application.Contracts.CoverPages;

namespace MngDocument.Infrastructure.Services;

/// <summary>Executive cover page DOCX skeleton with placeholder tokens.</summary>
public static class CoverPageDesignSkeletonBuilder
{
    public static byte[] Build(
        CoverPageDefinitionDto definition,
        CoverPageSettingsDto settings,
        string catalogName,
        byte[]? logoBytes = null,
        string logoExtension = ".png")
    {
        var blocks = new List<(string Text, int SizeHalfPoints, bool Bold, string Color)>();

        if (definition.ShowDocumentName)
            blocks.Add(("{{documentName}}", 48, true, "1F4E79"));
        if (definition.ShowCustomerName)
            blocks.Add(("{{customerName}}", 28, false, "44546A"));
        if (definition.ShowDocNo)
            blocks.Add(("{{docNo}}", 22, true, "2F5496"));
        if (definition.ShowGeneratedAt)
            blocks.Add(("{{generatedAt}}", 20, false, "7F7F7F"));

        if (blocks.Count == 0)
            blocks.Add((catalogName, 36, true, "1F4E79"));

        var documentXml = BuildDocumentXml(blocks);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RootRelsXml);
            WriteEntry(archive, "word/document.xml", documentXml);
            WriteEntry(archive, "word/_rels/document.xml.rels", DocumentRelsXml);
            WriteEntry(archive, "word/styles.xml", StylesXml);
            WriteEntry(archive, "word/settings.xml", SettingsXml);
        }

        var layout = TemplateModelSerializer.ToPageLayoutModel(settings.PageLayout)
                     ?? TemplatePageLayoutModel.CreateDefault();
        var result = PageLayoutInjector.Apply(ms.ToArray(), layout);

        if (definition.ShowLogo && logoBytes is { Length: > 0 })
            result = CoverPageLogoInjector.Apply(result, logoBytes, logoExtension);

        return result;
    }

    private static string BuildDocumentXml(IReadOnlyList<(string Text, int SizeHalfPoints, bool Bold, string Color)> blocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">""");
        sb.AppendLine("<w:body>");

        foreach (var (text, size, bold, color) in blocks)
        {
            sb.AppendLine("<w:p>");
            sb.AppendLine("  <w:pPr><w:spacing w:before=\"480\" w:after=\"120\"/><w:jc w:val=\"center\"/></w:pPr>");
            sb.AppendLine("  <w:r>");
            sb.Append("    <w:rPr>");
            sb.Append($"<w:sz w:val=\"{size}\"/><w:szCs w:val=\"{size}\"/>");
            if (bold)
                sb.Append("<w:b/>");
            sb.Append($"<w:color w:val=\"{color}\"/>");
            sb.AppendLine("</w:rPr>");
            sb.AppendLine($"    <w:t xml:space=\"preserve\">{EscapeXml(text)}</w:t>");
            sb.AppendLine("  </w:r>");
            sb.AppendLine("</w:p>");
        }

        sb.AppendLine("""
          <w:sectPr>
            <w:pgSz w:w="11906" w:h="16838"/>
            <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/>
          </w:sectPr>
          """);
        sb.AppendLine("</w:body>");
        sb.AppendLine("</w:document>");
        return sb.ToString();
    }
    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

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

    private const string RootRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
        </Relationships>
        """;

    private const string DocumentRelsXml =
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
}
