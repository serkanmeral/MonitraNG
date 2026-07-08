using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>İş paketi kontrol paneli DI şablonu — Kontrol Paneli (KPI + 2 chart) + Kalemler + Veri.</summary>
public static class PackageDashboardTemplateXlsxFactory
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>Tablo genişlemesi sonrası bar grafiğin kapsayacağı üst satır sınırı (Veri sayfası).</summary>
    private const int ChartDataRowCap = 120;

    public static byte[] Create()
    {
        var dashboardRows = BuildDashboardRows();
        var linesRows = BuildLinesRows();
        var dataRows = BuildDataRows();

        var dashboardDrawing = new XElement(Main + "drawing",
            new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
            new XAttribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"), "rId1"));

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteEntry(archive, "_rels/.rels", RelsXml);
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml",
                BuildWorksheet(dashboardRows, DashboardColWidths, DashboardMerges, dashboardDrawing));
            WriteEntry(archive, "xl/worksheets/sheet2.xml",
                BuildWorksheet(linesRows, LinesColWidths));
            WriteEntry(archive, "xl/worksheets/sheet3.xml",
                BuildWorksheet(dataRows, DataColWidths));
            WriteEntry(archive, "xl/worksheets/_rels/sheet1.xml.rels", Sheet1RelsXml);
            WriteEntry(archive, "xl/drawings/drawing1.xml", Drawing1Xml);
            WriteEntry(archive, "xl/drawings/_rels/drawing1.xml.rels", Drawing1RelsXml);
            WriteEntry(archive, "xl/charts/chart1.xml", LineComparisonChartXml);
            WriteEntry(archive, "xl/charts/chart2.xml", FulfillmentDonutChartXml);
            WriteEntry(archive, "xl/styles.xml", StylesXml);
        }

        return ms.ToArray();
    }

    private static readonly double[] DashboardColWidths =
        { 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12 };

    private static readonly double[] LinesColWidths =
        { 10, 14, 36, 12, 12, 12, 14, 10, 10, 16 };

    private static readonly double[] DataColWidths =
        { 12, 14, 14, 4, 14, 14 };

    private static readonly string[] DashboardMerges =
    {
        "A6:B6", "C6:D6", "E6:F6", "G6:H6", "I6:J6", "K6:L6",
        "A7:B7", "C7:D7", "E7:F7", "G7:H7", "I7:J7", "K7:L7",
        "D1:I1", "D2:I2", "D3:I3"
    };

    private static List<XElement> BuildDashboardRows()
    {
        return new List<XElement>
        {
            Row(1,
                Cell("A1", "", 0),
                Cell("D1", "İŞ PAKETİ KONTROL PANELİ", 1, inline: true),
                Cell("K1", "Liste: {{issueDate}}", 5, inline: true)),
            Row(2,
                Cell("D2", "{{packageNo}} — {{packageName}}", 6, inline: true)),
            Row(3,
                Cell("D3", "{{customerName}}", 5, inline: true),
                Cell("J3", "{{deliveryUrgencyLabel}}", 7, inline: true)),
            Row(4,
                Cell("D4", "Durum: {{statusLabel}}  ·  Termin: {{deliveryDate}}", 5, inline: true)),
            Row(5),
            Row(6,
                Cell("A6", "Kalem Sayısı", 3, inline: true),
                Cell("C6", "Sevkiyat", 3, inline: true),
                Cell("E6", "Tamamlanma", 3, inline: true),
                Cell("G6", "Açık NCR", 3, inline: true),
                Cell("I6", "Açık CAPA", 3, inline: true),
                Cell("K6", "Kalan Miktar", 3, inline: true)),
            Row(7,
                Cell("A7", "{{lineCount}}", 4, inline: true),
                Cell("C7", "{{shipmentSummary}}", 4, inline: true),
                Cell("E7", "{{fulfillmentPctLabel}}", 4, inline: true),
                Cell("G7", "{{openNcrCount}}", 4, inline: true),
                Cell("I7", "{{openCapaCount}}", 4, inline: true),
                Cell("K7", "{{remainingQuantity}}", 4, inline: true)),
            Row(8),
            Row(9,
                Cell("A9", "Başlangıç: {{beginDate}}", 5, inline: true),
                Cell("E9", "Sevk: {{shippedCount}} / {{partCount}} parça", 5, inline: true),
                Cell("I9", "Stok: {{stockCount}}", 5, inline: true)),
            Row(10,
                Cell("A10", "Üretim: {{generatedAt}}", 5, inline: true)),
        };
    }

    private static List<XElement> BuildLinesRows() =>
        new()
        {
            Row(1,
                Cell("A1", "Kalem No", 2, inline: true),
                Cell("B1", "PO Kalem", 2, inline: true),
                Cell("C1", "Tanım", 2, inline: true),
                Cell("D1", "Miktar", 2, inline: true),
                Cell("E1", "Sevk", 2, inline: true),
                Cell("F1", "Kalan", 2, inline: true),
                Cell("G1", "Termin", 2, inline: true),
                Cell("H1", "FAI", 2, inline: true),
                Cell("I1", "CoC", 2, inline: true),
                Cell("J1", "CoC No", 2, inline: true)),
            Row(2,
                Cell("A2", "{{packageLines.lineNo}}", 0, inline: true),
                Cell("B2", "{{packageLines.customerPoItemNo}}", 0, inline: true),
                Cell("C2", "{{packageLines.description}}", 0, inline: true),
                Cell("D2", "{{packageLines.quantity}}", 0, inline: true),
                Cell("E2", "{{packageLines.shippedQuantity}}", 0, inline: true),
                Cell("F2", "{{packageLines.remainingQuantity}}", 0, inline: true),
                Cell("G2", "{{packageLines.deliveryDate}}", 0, inline: true),
                Cell("H2", "{{packageLines.faiStatus}}", 0, inline: true),
                Cell("I2", "{{packageLines.cocStatus}}", 0, inline: true),
                Cell("J2", "{{packageLines.cocDocNo}}", 0, inline: true)),
        };

    private static List<XElement> BuildDataRows() =>
        new()
        {
            Row(1,
                Cell("E1", "Kategori", 2, inline: true),
                Cell("F1", "Miktar", 2, inline: true)),
            Row(2,
                Cell("E2", "{{donutSlices.category}}", 0, inline: true),
                Cell("F2", "{{donutSlices.amount}}", 0, inline: true)),
            Row(9,
                Cell("A9", "Kalem No", 2, inline: true),
                Cell("B9", "Sipariş", 2, inline: true),
                Cell("C9", "Sevk", 2, inline: true)),
            Row(10,
                Cell("A10", "{{chartLines.lineNo}}", 0, inline: true),
                Cell("B10", "{{chartLines.quantity}}", 0, inline: true),
                Cell("C10", "{{chartLines.shippedQuantity}}", 0, inline: true)),
        };

    private static string BuildWorksheet(
        List<XElement> rows,
        double[] colWidths,
        string[]? mergeRefs = null,
        params XElement[] extraElements)
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

        if (mergeRefs is { Length: > 0 })
        {
            ws.Add(new XElement(Main + "mergeCells",
                new XAttribute("count", mergeRefs.Length),
                mergeRefs.Select(r => new XElement(Main + "mergeCell", new XAttribute("ref", r)))));
        }

        foreach (var el in extraElements)
            ws.Add(el);

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

    private static string LineComparisonChartXml =>
        BuildClusteredColumnChart(
            title: "Kalem Bazlı Sipariş vs Sevk",
            categoryHeader: "Veri!$A$9",
            categories: $"Veri!$A$10:$A${ChartDataRowCap + 8}",
            series: new[]
            {
                ("Sipariş", "Veri!$B$9", $"Veri!$B$10:$B${ChartDataRowCap + 8}", "FF2E75B6"),
                ("Sevk", "Veri!$C$9", $"Veri!$C$10:$C${ChartDataRowCap + 8}", "FF70AD47")
            });

    private static string BuildClusteredColumnChart(
        string title,
        string categoryHeader,
        string categories,
        (string Label, string NameRef, string Values, string Color)[] series)
    {
        var seriesXml = string.Join('\n', series.Select((s, i) => $"""
                  <c:ser>
                    <c:idx val="{i}"/>
                    <c:order val="{i}"/>
                    <c:tx><c:strRef><c:f>{s.NameRef}</c:f><c:strCache><c:ptCount val="1"/><c:pt idx="0"><c:v>{s.Label}</c:v></c:pt></c:strCache></c:strRef></c:tx>
                    <c:spPr><a:solidFill><a:srgbClr val="{s.Color}"/></a:solidFill></c:spPr>
                    <c:cat><c:strRef><c:f>{categories}</c:f><c:strCache><c:ptCount val="1"/></c:strCache></c:strRef></c:cat>
                    <c:val><c:numRef><c:f>{s.Values}</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val="1"/></c:numCache></c:numRef></c:val>
                  </c:ser>
            """));

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
                          xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <c:chart>
                <c:title>
                  <c:tx><c:rich>
                    <a:bodyPr/><a:lstStyle/>
                    <a:p><a:r><a:rPr lang="tr-TR" sz="1200" b="1"/><a:t>{title}</a:t></a:r></a:p>
                  </c:rich></c:tx>
                  <c:overlay val="0"/>
                </c:title>
                <c:plotArea>
                  <c:layout/>
                  <c:barChart>
                    <c:barDir val="col"/>
                    <c:grouping val="clustered"/>
                    <c:varyColors val="0"/>
            {seriesXml}
                    <c:axId val="111111111"/>
                    <c:axId val="222222222"/>
                  </c:barChart>
                  <c:catAx>
                    <c:axId val="111111111"/>
                    <c:scaling><c:orientation val="minMax"/></c:scaling>
                    <c:axPos val="b"/>
                    <c:crossAx val="222222222"/>
                  </c:catAx>
                  <c:valAx>
                    <c:axId val="222222222"/>
                    <c:scaling><c:orientation val="minMax"/></c:scaling>
                    <c:axPos val="l"/>
                    <c:crossAx val="111111111"/>
                  </c:valAx>
                </c:plotArea>
                <c:legend><c:legendPos val="b"/><c:overlay val="0"/></c:legend>
              </c:chart>
            </c:chartSpace>
            """;
    }

    private static string BuildDoughnutChart(string title, string categories, string values) =>
        $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
                          xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <c:chart>
                <c:title>
                  <c:tx><c:rich>
                    <a:bodyPr/><a:lstStyle/>
                    <a:p><a:r><a:rPr lang="tr-TR" sz="1200" b="1"/><a:t>{title}</a:t></a:r></a:p>
                  </c:rich></c:tx>
                  <c:overlay val="0"/>
                </c:title>
                <c:plotArea>
                  <c:layout/>
                  <c:doughnutChart>
                    <c:varyColors val="1"/>
                    <c:ser>
                      <c:idx val="0"/>
                      <c:order val="0"/>
                      <c:tx><c:strRef><c:f>Veri!$F$1</c:f><c:strCache><c:ptCount val="1"/><c:pt idx="0"><c:v>Miktar</c:v></c:pt></c:strCache></c:strRef></c:tx>
                      <c:cat><c:strRef><c:f>{categories}</c:f><c:strCache><c:ptCount val="3"/><c:pt idx="0"><c:v>Sevk</c:v></c:pt><c:pt idx="1"><c:v>Kalan</c:v></c:pt><c:pt idx="2"><c:v>Stok</c:v></c:pt></c:strCache></c:strRef></c:cat>
                      <c:val><c:numRef><c:f>{values}</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val="3"/><c:pt idx="0"><c:v>1</c:v></c:pt><c:pt idx="1"><c:v>1</c:v></c:pt><c:pt idx="2"><c:v>1</c:v></c:pt></c:numCache></c:numRef></c:val>
                    </c:ser>
                    <c:firstSliceAng val="0"/>
                    <c:holeSize val="50"/>
                  </c:doughnutChart>
                </c:plotArea>
                <c:legend><c:legendPos val="r"/><c:overlay val="0"/></c:legend>
              </c:chart>
            </c:chartSpace>
            """;

    private static string FulfillmentDonutChartXml =>
        BuildDoughnutChart(
            title: "Genel Tamamlanma",
            categories: "Veri!$E$2:$E$4",
            values: "Veri!$F$2:$F$4");

    private const string ContentTypesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
          <Override PartName="/xl/charts/chart1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/>
          <Override PartName="/xl/charts/chart2.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/>
          <Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>
        </Types>
        """;

    private const string RelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string WorkbookXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Kontrol Paneli" sheetId="1" r:id="rId1"/>
            <sheet name="Kalemler" sheetId="2" r:id="rId2"/>
            <sheet name="Veri" sheetId="3" r:id="rId3"/>
          </sheets>
        </workbook>
        """;

    private const string WorkbookRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>
          <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string Sheet1RelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml"/>
        </Relationships>
        """;

    private const string Drawing1Xml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <xdr:twoCellAnchor>
            <xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>11</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>
            <xdr:to><xdr:col>6</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>28</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
            <xdr:graphicFrame macro="">
              <xdr:nvGraphicFramePr>
                <xdr:cNvPr id="2" name="Line Chart"/>
                <xdr:cNvGraphicFramePr/>
              </xdr:nvGraphicFramePr>
              <xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm>
              <a:graphic>
                <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">
                  <c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" r:id="rId1"/>
                </a:graphicData>
              </a:graphic>
            </xdr:graphicFrame>
            <xdr:clientData/>
          </xdr:twoCellAnchor>
          <xdr:twoCellAnchor>
            <xdr:from><xdr:col>6</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>11</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>
            <xdr:to><xdr:col>12</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>28</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
            <xdr:graphicFrame macro="">
              <xdr:nvGraphicFramePr>
                <xdr:cNvPr id="3" name="Donut Chart"/>
                <xdr:cNvGraphicFramePr/>
              </xdr:nvGraphicFramePr>
              <xdr:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></xdr:xfrm>
              <a:graphic>
                <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">
                  <c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" r:id="rId2"/>
                </a:graphicData>
              </a:graphic>
            </xdr:graphicFrame>
            <xdr:clientData/>
          </xdr:twoCellAnchor>
        </xdr:wsDr>
        """;

    private const string Drawing1RelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart2.xml"/>
        </Relationships>
        """;

    private const string StylesXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="4">
            <font><sz val="11"/><name val="Calibri"/><color theme="1"/></font>
            <font><b/><sz val="16"/><name val="Calibri"/><color theme="1"/></font>
            <font><b/><sz val="11"/><name val="Calibri"/><color theme="1"/></font>
            <font><b/><sz val="20"/><name val="Calibri"/><color theme="1"/></font>
          </fonts>
          <fills count="4">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFD9E2F3"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFF2F2F2"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border/>
            <border>
              <left style="thin"><color rgb="FFB4C6E7"/></left>
              <right style="thin"><color rgb="FFB4C6E7"/></right>
              <top style="thin"><color rgb="FFB4C6E7"/></top>
              <bottom style="thin"><color rgb="FFB4C6E7"/></bottom>
            </border>
          </borders>
          <cellStyleXfs count="1"><xf/></cellStyleXfs>
          <cellXfs count="8">
            <xf/>
            <xf fontId="1" applyFont="1"><alignment horizontal="center"/></xf>
            <xf fontId="2" applyFont="1"><alignment horizontal="center"/></xf>
            <xf fontId="2" applyFont="1" fillId="2" applyFill="1" borderId="1" applyBorder="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
            <xf fontId="3" applyFont="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf fontId="0" applyFont="1"><alignment horizontal="left"/></xf>
            <xf fontId="1" applyFont="1"><alignment horizontal="left"/></xf>
            <xf fontId="0" applyFont="1" fillId="3" applyFill="1"><alignment horizontal="center"/></xf>
          </cellXfs>
        </styleSheet>
        """;
}
