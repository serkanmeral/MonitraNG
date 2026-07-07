using System.IO.Compression;
using System.Text;

namespace MngDocument.Infrastructure.Services;

/// <summary>Collabora Impress demo — slayt geçişleri, KPI kartları, çubuk grafik şekilleri.</summary>
public static class CollaboraDemoPptxFactory
{
    private sealed record SlideContent(
        string Title,
        string[] Lines,
        bool ShowKpiCards = false,
        bool ShowBarChart = false,
        string Transition = "fade");

    private static readonly (string Label, int Value, string Color)[] DemoBars =
    {
        ("Oca", 12, "FF4472C4"),
        ("Mar", 15, "FF4472C4"),
        ("May", 11, "FF4472C4"),
        ("Tem", 14, "FF4472C4"),
        ("Eyl", 7, "FF4472C4"),
        ("Kas", 9, "FF4472C4"),
    };

    public static byte[] CreateDemo() => CreateDemo(DateTime.UtcNow);

    public static byte[] CreateDemo(DateTime generatedAt)
    {
        var slides = new[]
        {
            new SlideContent(
                "MonitraNG Document Intelligence",
                new[]
                {
                    "Kurumsal dokuman, tablo ve sunum yonetimi",
                    "Collabora Online ile tarayicida duzenleme",
                    $"Demo tarihi: {generatedAt:dd.MM.yyyy}"
                },
                Transition: "fade"),
            new SlideContent(
                "Platform yetenekleri",
                new[]
                {
                    "• Kaynak agaci — klasor, sayfa, dosya",
                    "• Native DOCX, elektronik tablo (XLSX), sunum (PPTX)",
                    "• Surum gecmisi ve ortak duzenleme (Collabora)",
                    "• Sablondan dokuman uretimi ve PDF disa aktarma",
                    "• Yetki matrisi ve is akisi entegrasyonu"
                },
                Transition: "push"),
            new SlideContent(
                "Canli KPI ornegi",
                Array.Empty<string>(),
                ShowKpiCards: true,
                Transition: "split"),
            new SlideContent(
                "Aylik alarm trendi",
                new[] { "Elektronik tablo Veriler sayfasi ile ayni veri seti" },
                ShowBarChart: true,
                Transition: "cover"),
            new SlideContent(
                "Elektronik tablo demosu",
                new[]
                {
                    "• 4 sayfa: Dashboard, Veriler, Ozet, Grafikler",
                    "• SUM, AVERAGE, MAX, COUNTIF, IF formulleri",
                    "• Gomulu sutun grafikleri ve veri cubuklari",
                    "• Collabora Calc'ta anlik hesaplama"
                },
                Transition: "wipe"),
            new SlideContent(
                "Canli demo akisi",
                new[]
                {
                    "1. Demo Elektronik Tablo'yu Collabora'da ac",
                    "2. Grafikler sayfasini ve formulleri goster",
                    "3. Sunumu slayt gosteriminde baslat (F5)",
                    "4. PDF disa aktar (DI veya Collabora menusu)",
                    "5. Surum gecmisinden onceki haline don"
                },
                Transition: "fade"),
            new SlideContent(
                "Tesekkurler",
                new[]
                {
                    "Sorulariniz?",
                    "MonitraNG — izleme ve dokuman zekasi",
                    "www.monitrang.com"
                },
                Transition: "push")
        };

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", BuildContentTypes(slides.Length));
            Write(archive, "_rels/.rels", RootRels());
            Write(archive, "ppt/presentation.xml", BuildPresentation(slides.Length));
            Write(archive, "ppt/_rels/presentation.xml.rels", BuildPresentationRels(slides.Length));
            Write(archive, "ppt/slideLayouts/slideLayout1.xml", SlideLayoutXml);
            Write(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", SlideLayoutRelsXml);
            Write(archive, "ppt/slideMasters/slideMaster1.xml", SlideMasterXml);
            Write(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", SlideMasterRelsXml);
            Write(archive, "ppt/theme/theme1.xml", ThemeXml);

            for (var i = 0; i < slides.Length; i++)
            {
                var n = i + 1;
                Write(archive, $"ppt/slides/slide{n}.xml", BuildSlide(slides[i]));
                Write(archive, $"ppt/slides/_rels/slide{n}.xml.rels", SlideRelsXml);
            }
        }

        return ms.ToArray();
    }

    private static string BuildContentTypes(int slideCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        sb.AppendLine("""  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        sb.AppendLine("""  <Default Extension="xml" ContentType="application/xml"/>""");
        sb.AppendLine("""  <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>""");
        for (var i = 1; i <= slideCount; i++)
            sb.AppendLine($"""  <Override PartName="/ppt/slides/slide{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slide+xml"/>""");
        sb.AppendLine("""  <Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/>""");
        sb.AppendLine("""  <Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/>""");
        sb.AppendLine("""  <Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>""");
        sb.AppendLine("""</Types>""");
        return sb.ToString();
    }

    private static string BuildPresentation(int slideCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">""");
        sb.AppendLine("""  <p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst>""");
        sb.AppendLine("  <p:sldIdLst>");
        for (var i = 0; i < slideCount; i++)
            sb.AppendLine($"""    <p:sldId id="{256 + i}" r:id="rId{i + 2}"/>""");
        sb.AppendLine("  </p:sldIdLst>");
        sb.AppendLine("""  <p:sldSz cx="12192000" cy="6858000"/>""");
        sb.AppendLine("""  <p:notesSz cx="6858000" cy="9144000"/>""");
        sb.AppendLine("""</p:presentation>""");
        return sb.ToString();
    }

    private static string BuildPresentationRels(int slideCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        sb.AppendLine("""  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="slideMasters/slideMaster1.xml"/>""");
        for (var i = 0; i < slideCount; i++)
            sb.AppendLine($"""  <Relationship Id="rId{i + 2}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide{i + 1}.xml"/>""");
        sb.AppendLine($"""  <Relationship Id="rId{slideCount + 2}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="theme/theme1.xml"/>""");
        sb.AppendLine("""</Relationships>""");
        return sb.ToString();
    }

    private static string BuildSlide(SlideContent slide)
    {
        var shapes = new StringBuilder();
        shapes.AppendLine("""              <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>""");
        shapes.AppendLine("""              <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>""");

        var shapeId = 2;
        shapes.Append(TextShape(shapeId++, slide.Title, 838200, 457200, 10515600, 1143000, 4400, true, "FF1F4E79"));

        if (slide.ShowKpiCards)
        {
            shapes.Append(KpiCard(shapeId++, "Uptime", "99.4%", 838200, 2006600, 3200000, 1800000, "FF2F5496"));
            shapes.Append(KpiCard(shapeId++, "Alarmlar", "118", 4560000, 2006600, 3200000, 1800000, "FFED7D31"));
            shapes.Append(KpiCard(shapeId++, "Enerji", "14.8 GWh", 8280000, 2006600, 3200000, 1800000, "FF70AD47"));
            shapes.Append(TextShape(shapeId, "Aylik tesis metrikleri — Collabora Calc ile canli baglanabilir", 838200, 4200000, 10515600, 800000, 2000, false, "FF44546A"));
        }
        else if (slide.ShowBarChart)
        {
            shapes.Append(BarChartShapes(ref shapeId, 1200000, 2200000, 9600000, 3600000));
            if (slide.Lines.Length > 0)
                shapes.Append(TextShape(shapeId, string.Join("\n", slide.Lines), 838200, 6000000, 10515600, 600000, 2000, false, "FF7F7F7F"));
        }
        else if (slide.Lines.Length > 0)
        {
            shapes.Append(TextShape(shapeId, string.Join("\n", slide.Lines), 838200, 1900000, 10515600, 4200000, 2400, false, "FF44546A"));
        }

        var transition = BuildTransition(slide.Transition);

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                   xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld>
                <p:spTree>
            {shapes}
                </p:spTree>
              </p:cSld>
              <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
            {transition}
            </p:sld>
            """;
    }

    private static string BuildTransition(string type) => type switch
    {
        "push" => """
              <p:transition spd="med" advClick="1">
                <p:push dir="l"/>
              </p:transition>
            """,
        "split" => """
              <p:transition spd="med" advClick="1">
                <p:split orient="vert" dir="in"/>
              </p:transition>
            """,
        "cover" => """
              <p:transition spd="med" advClick="1">
                <p:cover dir="lt"/>
              </p:transition>
            """,
        "wipe" => """
              <p:transition spd="med" advClick="1">
                <p:blinds dim="vert"/>
              </p:transition>
            """,
        _ => """
              <p:transition spd="med" advClick="1">
                <p:fade/>
              </p:transition>
            """
    };

    private static string BarChartShapes(ref int shapeId, long originX, long originY, long width, long height)
    {
        var sb = new StringBuilder();
        var maxVal = DemoBars.Max(b => b.Value);
        var barWidth = width / (DemoBars.Length * 2);
        var gap = barWidth;
        var chartBottom = originY + height;

        sb.AppendLine(RectShape(shapeId++, originX, originY, width, height, "FFF8F8F8", "FFD9D9D9", 9525));
        sb.AppendLine(LineShape(shapeId++, originX + 800000, chartBottom - 200000, originX + width - 200000, chartBottom - 200000, "FF44546A"));

        for (var i = 0; i < DemoBars.Length; i++)
        {
            var bar = DemoBars[i];
            var barHeight = (long)(height * 0.72 * bar.Value / maxVal);
            var x = originX + gap + i * (barWidth + gap);
            var y = chartBottom - 200000 - barHeight;
            sb.AppendLine(RectShape(shapeId++, x, y, barWidth, barHeight, bar.Color, bar.Color, 0));
            sb.AppendLine(TextShape(shapeId++, bar.Label, x - 50000, chartBottom - 120000, barWidth + 100000, 400000, 1600, false, "FF44546A"));
            sb.AppendLine(TextShape(shapeId++, bar.Value.ToString(), x - 50000, y - 350000, barWidth + 100000, 300000, 1400, true, "FF1F4E79"));
        }

        sb.AppendLine(TextShape(shapeId, "Aylik alarm sayisi (6 ay ornek)", originX, originY - 200000, width, 400000, 2200, true, "FF2F5496"));
        return sb.ToString();
    }

    private static string RectShape(int id, long x, long y, long cx, long cy, string fill, string line, int lineWidth) =>
        $"""
                      <p:sp>
                        <p:nvSpPr><p:cNvPr id="{id}" name="Shape {id}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
                        <p:spPr>
                          <a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>
                          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                          <a:solidFill><a:srgbClr val="{fill}"/></a:solidFill>
                          <a:ln w="{lineWidth}"><a:solidFill><a:srgbClr val="{line}"/></a:solidFill></a:ln>
                        </p:spPr>
                        <p:txBody><a:bodyPr/><a:lstStyle/></p:txBody>
                      </p:sp>
            """;

    private static string LineShape(int id, long x1, long y1, long x2, long y2, string color) =>
        $"""
                      <p:cxnSp>
                        <p:nvCxnSpPr>
                          <p:cNvPr id="{id}" name="Line {id}"/>
                          <p:cNvCxnSpPr/>
                          <p:nvPr/>
                        </p:nvCxnSpPr>
                        <p:spPr>
                          <a:xfrm>
                            <a:off x="{Math.Min(x1, x2)}" y="{Math.Min(y1, y2)}"/>
                            <a:ext cx="{Math.Abs(x2 - x1)}" cy="{Math.Abs(y2 - y1)}"/>
                          </a:xfrm>
                          <a:prstGeom prst="line"><a:avLst/></a:prstGeom>
                          <a:ln w="19050"><a:solidFill><a:srgbClr val="{color}"/></a:solidFill></a:ln>
                        </p:spPr>
                      </p:cxnSp>
            """;

    private static string TextShape(int id, string text, long x, long y, long cx, long cy, int sz, bool bold, string colorRgb)
    {
        var paragraphs = new StringBuilder();
        foreach (var line in text.Split('\n'))
        {
            paragraphs.AppendLine("""                  <a:p>""");
            paragraphs.AppendLine($"""                    <a:r><a:rPr lang="tr-TR" sz="{sz}"{(bold ? " b=\"1\"" : "")}><a:solidFill><a:srgbClr val="{colorRgb}"/></a:solidFill></a:rPr><a:t>{Esc(line)}</a:t></a:r>""");
            paragraphs.AppendLine("""                  </a:p>""");
        }

        return $"""
                      <p:sp>
                        <p:nvSpPr>
                          <p:cNvPr id="{id}" name="TextBox {id}"/>
                          <p:cNvSpPr txBox="1"/>
                          <p:nvPr/>
                        </p:nvSpPr>
                        <p:spPr>
                          <a:xfrm>
                            <a:off x="{x}" y="{y}"/>
                            <a:ext cx="{cx}" cy="{cy}"/>
                          </a:xfrm>
                          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                          <a:noFill/>
                        </p:spPr>
                        <p:txBody>
                          <a:bodyPr wrap="square" rtlCol="0"/>
                          <a:lstStyle/>
            {paragraphs}
                        </p:txBody>
                      </p:sp>
            """;
    }

    private static string KpiCard(int id, string label, string value, long x, long y, long cx, long cy, string accent)
    {
        return $"""
                      <p:sp>
                        <p:nvSpPr>
                          <p:cNvPr id="{id}" name="KPI {id}"/>
                          <p:cNvSpPr/>
                          <p:nvPr/>
                        </p:nvSpPr>
                        <p:spPr>
                          <a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>
                          <a:prstGeom prst="roundRect"><a:avLst/></a:prstGeom>
                          <a:solidFill><a:srgbClr val="FFF2F2F2"/></a:solidFill>
                          <a:ln w="12700"><a:solidFill><a:srgbClr val="{accent}"/></a:solidFill></a:ln>
                        </p:spPr>
                        <p:txBody>
                          <a:bodyPr anchor="ctr"/>
                          <a:lstStyle/>
                          <a:p>
                            <a:pPr algn="ctr"/>
                            <a:r><a:rPr lang="tr-TR" sz="2000" b="1"><a:solidFill><a:srgbClr val="{accent}"/></a:solidFill></a:rPr><a:t>{Esc(label)}</a:t></a:r>
                          </a:p>
                          <a:p>
                            <a:pPr algn="ctr"/>
                            <a:r><a:rPr lang="tr-TR" sz="3600" b="1"><a:solidFill><a:srgbClr val="FF1F4E79"/></a:solidFill></a:rPr><a:t>{Esc(value)}</a:t></a:r>
                          </a:p>
                        </p:txBody>
                      </p:sp>
            """;
    }

    private static string Esc(string s) =>
        s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private const string RootRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>
        </Relationships>
        """;

    private static string RootRels() => RootRelsXml;

    private const string SlideRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
        </Relationships>
        """;

    private const string SlideLayoutXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                     xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                     xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                     type="blank" preserve="1">
          <p:cSld name="Blank">
            <p:spTree>
              <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
              <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
            </p:spTree>
          </p:cSld>
          <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
        </p:sldLayout>
        """;

    private const string SlideLayoutRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml"/>
        </Relationships>
        """;

    private const string SlideMasterXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                     xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                     xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
          <p:cSld>
            <p:bg><p:bgRef idx="1001"><a:schemeClr val="bg1"/></p:bgRef></p:bg>
            <p:spTree>
              <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
              <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
            </p:spTree>
          </p:cSld>
          <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2" accent1="accent1" accent2="accent2"
                    accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6"
                    hlink="hlink" folHlink="folHlink"/>
          <p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst>
        </p:sldMaster>
        """;

    private const string SlideMasterRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml"/>
        </Relationships>
        """;

    private const string ThemeXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="MonitraNG">
          <a:themeElements>
            <a:clrScheme name="MonitraNG">
              <a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1>
              <a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>
              <a:dk2><a:srgbClr val="44546A"/></a:dk2>
              <a:lt2><a:srgbClr val="E7E6E6"/></a:lt2>
              <a:accent1><a:srgbClr val="4472C4"/></a:accent1>
              <a:accent2><a:srgbClr val="ED7D31"/></a:accent2>
              <a:accent3><a:srgbClr val="70AD47"/></a:accent3>
              <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
              <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
              <a:accent6><a:srgbClr val="A5A5A5"/></a:accent6>
              <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
              <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="Office">
              <a:majorFont><a:latin typeface="Calibri Light"/></a:majorFont>
              <a:minorFont><a:latin typeface="Calibri"/></a:minorFont>
            </a:fontScheme>
            <a:fmtScheme name="Office">
              <a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:fillStyleLst>
              <a:lnStyleLst><a:ln w="9525"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:ln></a:lnStyleLst>
              <a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>
              <a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill></a:bgFillStyleLst>
            </a:fmtScheme>
          </a:themeElements>
        </a:theme>
        """;

    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
