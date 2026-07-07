using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>Collabora Calc demo — KPI, formüller, koşullu biçimlendirme, gömülü grafikler.</summary>
public static class CollaboraDemoXlsxFactory
{
    public static byte[] CreateDemo() => CreateDemo(DateTime.UtcNow);

    public static byte[] CreateDemo(DateTime generatedAt)
    {
        var months = new[] { "Oca", "Sub", "Mar", "Nis", "May", "Haz", "Tem", "Agu", "Eyl", "Eki", "Kas", "Ara" };
        var alarms = new[] { 12, 9, 15, 8, 11, 6, 14, 10, 7, 13, 9, 5 };
        var uptime = new[] { 99.1, 99.4, 98.8, 99.6, 99.2, 99.7, 99.0, 99.5, 99.8, 99.3, 99.1, 99.9 };
        var energy = new[] { 1240, 1180, 1310, 1275, 1190, 1220, 1350, 1288, 1210, 1295, 1175, 1130 };

        var veriler = BuildVerilerSheet(months, alarms, uptime, energy);
        var dashboard = BuildDashboardSheet(generatedAt);
        var ozet = BuildOzetSheet();
        var grafikler = BuildGrafiklerSheet();

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes());
            Write(archive, "_rels/.rels", RootRels());
            Write(archive, "xl/workbook.xml", Workbook());
            Write(archive, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Write(archive, "xl/styles.xml", Styles());
            Write(archive, "xl/worksheets/sheet1.xml", dashboard);
            Write(archive, "xl/worksheets/sheet2.xml", veriler);
            Write(archive, "xl/worksheets/sheet3.xml", ozet);
            Write(archive, "xl/worksheets/sheet4.xml", grafikler);
            Write(archive, "xl/worksheets/_rels/sheet4.xml.rels", Sheet4Rels());
            Write(archive, "xl/drawings/drawing1.xml", Drawing1());
            Write(archive, "xl/drawings/_rels/drawing1.xml.rels", Drawing1Rels());
            Write(archive, "xl/charts/chart1.xml", AlarmChart());
            Write(archive, "xl/charts/chart2.xml", EnergyChart());
        }

        return ms.ToArray();
    }

    private static string BuildDashboardSheet(DateTime generatedAt)
    {
        var rows = new List<XElement>
        {
            Row(1, Cell("A1", "MonitraNG — Tesis KPI Panosu", 1, inline: true), Cell("E1", $"Guncelleme: {generatedAt:dd.MM.yyyy HH:mm}", 0, inline: true)),
            Row(2),
            Row(3, Cell("A3", "Gosterge", 1, inline: true), Cell("B3", "Deger", 1, inline: true), Cell("C3", "Hedef", 1, inline: true), Cell("D3", "Durum", 1, inline: true)),
            Row(4, Cell("A4", "Toplam alarm (yil)", 0, inline: true), Cell("B4", null, 3, formula: "SUM(Veriler!B2:B13)"), Cell("C4", "120", 3), Cell("D4", null, 0, formula: "IF(B4<=C4,\"OK\",\"Incele\")", inline: true)),
            Row(5, Cell("A5", "Ortalama uptime %", 0, inline: true), Cell("B5", null, 5, formula: "AVERAGE(Veriler!C2:C13)"), Cell("C5", "99.0", 5), Cell("D5", null, 0, formula: "IF(B5>=C5,\"OK\",\"Incele\")", inline: true)),
            Row(6, Cell("A6", "Enerji tuketimi (MWh)", 0, inline: true), Cell("B6", null, 3, formula: "SUM(Veriler!D2:D13)"), Cell("C6", "15000", 3), Cell("D6", null, 0, formula: "IF(B6<=C6,\"OK\",\"Incele\")", inline: true)),
            Row(7, Cell("A7", "En yuksek alarm", 0, inline: true), Cell("B7", null, 3, formula: "MAX(Veriler!B2:B13)"), Cell("C7", "20", 3), Cell("D7", null, 0, formula: "IF(B7<=C7,\"OK\",\"Incele\")", inline: true)),
            Row(8),
            Row(9, Cell("A9", "Grafikler sayfasinda gomulu sutun grafikleri bulunur.", 0, inline: true)),
            Row(10, Cell("A10", "Veriler sayfasinda alarm ve enerji icin veri cubuklari uygulanir.", 0, inline: true)),
        };

        return Worksheet(rows, freezeRow: 3, cols: new[] { 22.0, 14.0, 12.0, 12.0 });
    }

    private static string BuildVerilerSheet(string[] months, int[] alarms, double[] uptime, int[] energy)
    {
        var rows = new List<XElement>
        {
            Row(1, Cell("A1", "Ay", 1, inline: true), Cell("B1", "Alarm", 1, inline: true), Cell("C1", "Uptime %", 1, inline: true), Cell("D1", "Enerji MWh", 1, inline: true), Cell("E1", "Trend", 1, inline: true)),
        };

        for (var i = 0; i < months.Length; i++)
        {
            var r = i + 2;
            var prev = i == 0 ? energy[i] : energy[i - 1];
            rows.Add(Row(r,
                Cell($"A{r}", months[i], 0, inline: true),
                Cell($"B{r}", alarms[i].ToString(), 3),
                Cell($"C{r}", uptime[i].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), 5),
                Cell($"D{r}", energy[i].ToString(), 3),
                Cell($"E{r}", null, 0, formula: $"IF(D{r}>{prev},\"Yukselis\",\"Dusus\")", inline: true)));
        }

        rows.Add(Row(14));
        rows.Add(Row(15, Cell("A15", "Yillik toplam / ortalama", 1, inline: true)));
        rows.Add(Row(16,
            Cell("A16", "Toplam", 0, inline: true),
            Cell("B16", null, 3, formula: "SUM(B2:B13)"),
            Cell("C16", null, 5, formula: "AVERAGE(C2:C13)"),
            Cell("D16", null, 3, formula: "SUM(D2:D13)")));

        var dataBars = new XElement(XName.Get("conditionalFormatting", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute("sqref", "B2:B13"),
            new XElement(XName.Get("cfRule", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                new XAttribute("type", "dataBar"),
                new XAttribute("priority", 1),
                new XElement(XName.Get("dataBar", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                    new XElement(XName.Get("cfvo", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), new XAttribute("type", "min")),
                    new XElement(XName.Get("cfvo", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), new XAttribute("type", "max")),
                    new XElement(XName.Get("color", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), new XAttribute("rgb", "FF4472C4")))));

        var energyBars = new XElement(XName.Get("conditionalFormatting", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute("sqref", "D2:D13"),
            new XElement(XName.Get("cfRule", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                new XAttribute("type", "dataBar"),
                new XAttribute("priority", 2),
                new XElement(XName.Get("dataBar", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                    new XElement(XName.Get("cfvo", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), new XAttribute("type", "min")),
                    new XElement(XName.Get("cfvo", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), new XAttribute("type", "max")),
                    new XElement(XName.Get("color", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), new XAttribute("rgb", "FF70AD47")))));

        return Worksheet(rows, freezeRow: 1, cols: new[] { 10.0, 12.0, 12.0, 14.0, 10.0 }, extraElements: [dataBars, energyBars]);
    }

    private static string BuildOzetSheet()
    {
        var rows = new List<XElement>
        {
            Row(1, Cell("A1", "Alarm esik analizi (COUNTIF)", 1, inline: true)),
            Row(2, Cell("A2", "Esik", 1, inline: true), Cell("B2", "Adet", 1, inline: true)),
            Row(3, Cell("A3", ">= 12", 0, inline: true), Cell("B3", null, 3, formula: "COUNTIF(Veriler!B2:B13,\">=12\")")),
            Row(4, Cell("A4", "< 10", 0, inline: true), Cell("B4", null, 3, formula: "COUNTIF(Veriler!B2:B13,\"<10\")")),
            Row(5),
            Row(6, Cell("A6", "Uptime bandi", 1, inline: true)),
            Row(7, Cell("A7", ">= 99.5%", 0, inline: true), Cell("B7", null, 3, formula: "COUNTIF(Veriler!C2:C13,\">=99.5\")")),
            Row(8, Cell("A8", "< 99.0%", 0, inline: true), Cell("B8", null, 3, formula: "COUNTIF(Veriler!C2:C13,\"<99\")")),
            Row(9),
            Row(10, Cell("A10", "Grafikler sayfasindaki grafikler bu verilerden beslenir.", 0, inline: true)),
        };

        return Worksheet(rows, freezeRow: 0, cols: new[] { 18.0, 12.0 });
    }

    private static string BuildGrafiklerSheet()
    {
        var rows = new List<XElement>
        {
            Row(1, Cell("A1", "Gomulu grafikler (Veriler sayfasina bagli)", 2, inline: true)),
            Row(2, Cell("A2", "Alarm sutun grafigi (sol) — Enerji sutun grafigi (sag)", 0, inline: true)),
        };

        var drawing = new XElement(XName.Get("drawing", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"), "rId1"));

        return Worksheet(rows, freezeRow: 0, cols: new[] { 48.0 }, extraElements: [drawing]);
    }

    private static string Worksheet(List<XElement> rows, int freezeRow, double[] cols, XElement[]? extraElements = null)
    {
        var colDefs = cols.Select((w, i) => new XElement(XName.Get("col", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute("min", i + 1), new XAttribute("max", i + 1), new XAttribute("width", w), new XAttribute("customWidth", 1)));

        var sheetData = new XElement(XName.Get("sheetData", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), rows);

        var ws = new XElement(XName.Get("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"));

        if (freezeRow > 0)
        {
            ws.Add(new XElement(XName.Get("sheetViews", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                new XElement(XName.Get("sheetView", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                    new XAttribute("workbookViewId", 0),
                    new XElement(XName.Get("pane", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                        new XAttribute("ySplit", freezeRow),
                        new XAttribute("topLeftCell", $"A{freezeRow + 1}"),
                        new XAttribute("activePane", "bottomLeft"),
                        new XAttribute("state", "frozen")),
                    new XElement(XName.Get("selection", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                        new XAttribute("pane", "bottomLeft"),
                        new XAttribute("activeCell", "A1"),
                        new XAttribute("sqref", "A1")))));
        }

        ws.Add(new XElement(XName.Get("cols", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), colDefs));
        ws.Add(sheetData);

        if (extraElements is not null)
        {
            foreach (var el in extraElements)
                ws.Add(el);
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + ws;
    }

    private static XElement Row(int index, params XElement[] cells) =>
        new(XName.Get("row", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute("r", index),
            cells);

    private static XElement Cell(string addr, string? value, int style, string? formula = null, bool inline = false)
    {
        var c = new XElement(XName.Get("c", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
            new XAttribute("r", addr),
            new XAttribute("s", style));

        if (formula is not null)
        {
            c.Add(new XElement(XName.Get("f", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), formula));
            c.Add(new XElement(XName.Get("v", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), "0"));
            return c;
        }

        if (inline)
        {
            c.Add(new XAttribute("t", "inlineStr"));
            c.Add(new XElement(XName.Get("is", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                new XElement(XName.Get("t", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), value ?? string.Empty)));
            return c;
        }

        c.Add(new XElement(XName.Get("v", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"), value ?? "0"));
        return c;
    }

    private static string ContentTypes() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet3.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet4.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
          <Override PartName="/xl/charts/chart1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/>
          <Override PartName="/xl/charts/chart2.xml" ContentType="application/vnd.openxmlformats-officedocument.drawingml.chart+xml"/>
          <Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>
        </Types>
        """;

    private static string RootRels() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string Workbook() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Dashboard" sheetId="1" r:id="rId1"/>
            <sheet name="Veriler" sheetId="2" r:id="rId2"/>
            <sheet name="Ozet" sheetId="3" r:id="rId3"/>
            <sheet name="Grafikler" sheetId="4" r:id="rId4"/>
          </sheets>
        </workbook>
        """;

    private static string WorkbookRels() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet3.xml"/>
          <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet4.xml"/>
          <Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private static string Sheet4Rels() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml"/>
        </Relationships>
        """;

    private static string Drawing1() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
                  xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <xdr:twoCellAnchor>
            <xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>3</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>
            <xdr:to><xdr:col>6</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>18</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
            <xdr:graphicFrame macro="">
              <xdr:nvGraphicFramePr>
                <xdr:cNvPr id="2" name="Alarm Chart"/>
                <xdr:cNvGraphicFramePr/>
              </xdr:nvGraphicFramePr>
              <xdr:xfrm>
                <a:off x="0" y="0"/><a:ext cx="0" cy="0"/>
              </xdr:xfrm>
              <a:graphic>
                <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/chart">
                  <c:chart xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart" r:id="rId1"/>
                </a:graphicData>
              </a:graphic>
            </xdr:graphicFrame>
            <xdr:clientData/>
          </xdr:twoCellAnchor>
          <xdr:twoCellAnchor>
            <xdr:from><xdr:col>7</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>3</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>
            <xdr:to><xdr:col>13</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>18</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
            <xdr:graphicFrame macro="">
              <xdr:nvGraphicFramePr>
                <xdr:cNvPr id="3" name="Energy Chart"/>
                <xdr:cNvGraphicFramePr/>
              </xdr:nvGraphicFramePr>
              <xdr:xfrm>
                <a:off x="0" y="0"/><a:ext cx="0" cy="0"/>
              </xdr:xfrm>
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

    private static string Drawing1Rels() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart" Target="../charts/chart2.xml"/>
        </Relationships>
        """;

    private static string AlarmChart() => BuildColumnChart("Aylik Alarmlar", "Veriler!$B$1", "Veriler!$A$2:$A$13", "Veriler!$B$2:$B$13", "FF4472C4");

    private static string EnergyChart() => BuildColumnChart("Enerji Tuketimi (MWh)", "Veriler!$D$1", "Veriler!$A$2:$A$13", "Veriler!$D$2:$D$13", "FF70AD47");

    private static string BuildColumnChart(string title, string seriesName, string categories, string values, string color)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <c:chartSpace xmlns:c="http://schemas.openxmlformats.org/drawingml/2006/chart"
                          xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <c:chart>
                <c:title>
                  <c:tx><c:rich>
                    <a:bodyPr/><a:lstStyle/>
                    <a:p><a:r><a:rPr lang="tr-TR" sz="1400" b="1"/><a:t>{title}</a:t></a:r></a:p>
                  </c:rich></c:tx>
                  <c:overlay val="0"/>
                </c:title>
                <c:plotArea>
                  <c:layout/>
                  <c:barChart>
                    <c:barDir val="col"/>
                    <c:grouping val="clustered"/>
                    <c:varyColors val="0"/>
                    <c:ser>
                      <c:idx val="0"/>
                      <c:order val="0"/>
                      <c:tx><c:strRef><c:f>{seriesName}</c:f><c:strCache><c:ptCount val="1"/><c:pt idx="0"><c:v>Seri</c:v></c:pt></c:strCache></c:strRef></c:tx>
                      <c:spPr><a:solidFill><a:srgbClr val="{color}"/></a:solidFill></c:spPr>
                      <c:cat><c:strRef><c:f>{categories}</c:f><c:strCache><c:ptCount val="12"/></c:strCache></c:strRef></c:cat>
                      <c:val><c:numRef><c:f>{values}</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val="12"/></c:numCache></c:numRef></c:val>
                    </c:ser>
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
                <c:legend><c:legendPos val="r"/><c:overlay val="0"/></c:legend>
              </c:chart>
            </c:chartSpace>
            """;
    }

    private static string Styles() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="3">
            <numFmt numFmtId="164" formatCode="#,##0"/>
            <numFmt numFmtId="165" formatCode="0.0%"/>
            <numFmt numFmtId="166" formatCode="0.0"/>
          </numFmts>
          <fonts count="3">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="16"/><color rgb="FF1F4E79"/><name val="Calibri"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF2F5496"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border/>
            <border>
              <left style="thin"><color auto="1"/></left>
              <right style="thin"><color auto="1"/></right>
              <top style="thin"><color auto="1"/></top>
              <bottom style="thin"><color auto="1"/></bottom>
            </border>
          </borders>
          <cellStyleXfs count="1"><xf/></cellStyleXfs>
          <cellXfs count="6">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="1" applyFont="1" applyFill="1" applyBorder="1"/>
            <xf numFmtId="0" fontId="2" fillId="0" borderId="0" applyFont="1"/>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" applyNumberFormat="1" applyBorder="1"/>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="1" applyNumberFormat="1" applyBorder="1"/>
            <xf numFmtId="166" fontId="0" fillId="0" borderId="1" applyNumberFormat="1" applyBorder="1"/>
          </cellXfs>
        </styleSheet>
        """;

    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
