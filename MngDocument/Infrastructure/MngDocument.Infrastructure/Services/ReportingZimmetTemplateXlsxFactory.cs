using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>Zimmet rapor belge şablonları (XLSX) — personel dökümü + teslim/iade tutanakları.</summary>
public static class ReportingZimmetTemplateXlsxFactory
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>Personel zimmet dökümü (reportRun) — tablo satırları.</summary>
    public static byte[] CreatePersonelZimmetDokumu() =>
        CreateList(
            sheetName: "PersonelZimmet",
            title: "Personel zimmet dökümü",
            headers: new[]
            {
                "Demirbaş no", "Ürün", "Seri no", "Durum", "Zimmet WI", "Garanti bitiş"
            },
            rowPlaceholders: new[]
            {
                "{{rows.demirbasNo}}", "{{rows.ad}}", "{{rows.seriNo}}", "{{rows.durum}}",
                "{{rows.zimmetRef}}", "{{rows.garantiBitis}}"
            },
            colWidths: new[] { 14.0, 28.0, 16.0, 12.0, 14.0, 14.0 });

    private static byte[] CreateList(
        string sheetName,
        string title,
        string[] headers,
        string[] rowPlaceholders,
        double[] colWidths)
    {
        var headerCells = headers
            .Select((h, i) => Cell($"{(char)('A' + i)}6", h, 2, inline: true))
            .ToArray();
        var rowCells = rowPlaceholders
            .Select((p, i) => Cell($"{(char)('A' + i)}7", p, 0, inline: true))
            .ToArray();

        var rows = new List<XElement>
        {
            Row(1, Cell("A1", title, 1, inline: true)),
            Row(2,
                Cell("A2", "Rapor:", 0, inline: true),
                Cell("B2", "{{reportTitle}}", 0, inline: true)),
            Row(3,
                Cell("A3", "Filtreler:", 0, inline: true),
                Cell("B3", "{{filtersSummary}}", 0, inline: true)),
            Row(4,
                Cell("A4", "Üretim:", 0, inline: true),
                Cell("B4", "{{generatedAt}}", 0, inline: true),
                Cell("C4", "Satır:", 0, inline: true),
                Cell("D4", "{{rowCount}}", 0, inline: true)),
            Row(5),
            Row(6, headerCells),
            Row(7, rowCells)
        };

        return Pack(sheetName, BuildWorksheet(rows, colWidths));
    }

    private static byte[] Pack(string sheetName, string sheetXml)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RelsXml);
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml(sheetName));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
            WriteEntry(archive, "xl/styles.xml", StylesXml);
        }

        return ms.ToArray();
    }

    private static string BuildWorksheet(List<XElement> rows, double[] colWidths)
    {
        var colDefs = colWidths.Select((w, i) => new XElement(Main + "col",
            new XAttribute("min", i + 1),
            new XAttribute("max", i + 1),
            new XAttribute("width", w),
            new XAttribute("customWidth", 1)));

        var sheetData = new XElement(Main + "sheetData", rows);
        var ws = new XElement(Main + "worksheet",
            new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
            new XElement(Main + "cols", colDefs),
            sheetData);

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + ws;
    }

    private static XElement Row(int index, params XElement[] cells) =>
        new(Main + "row", new XAttribute("r", index), cells);

    private static XElement Cell(string addr, string? value, int style, bool inline = false)
    {
        var c = new XElement(Main + "c",
            new XAttribute("r", addr),
            new XAttribute("s", style));

        if (inline)
        {
            c.Add(new XAttribute("t", "inlineStr"));
            c.Add(new XElement(Main + "is",
                new XElement(Main + "t", value ?? string.Empty)));
        }

        return c;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string WorkbookXml(string sheetName) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="{sheetName}" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private const string ContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string RelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string WorkbookRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string StylesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="14"/><name val="Calibri"/></font>
          </fonts>
          <fills count="2">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
          </fills>
          <borders count="1"><border/></borders>
          <cellStyleXfs count="1"><xf/></cellStyleXfs>
          <cellXfs count="3">
            <xf/>
            <xf fontId="1" applyFont="1"/>
            <xf fontId="0" applyFont="1"><alignment horizontal="center"/></xf>
          </cellXfs>
        </styleSheet>
        """;
}
