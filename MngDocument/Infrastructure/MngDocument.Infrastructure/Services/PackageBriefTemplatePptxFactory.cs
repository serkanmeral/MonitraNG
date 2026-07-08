using System.IO.Compression;
using System.Text;

namespace MngDocument.Infrastructure.Services;

/// <summary>İş paketi müşteri sunumu PPTX — executive KPI slaytları (PACKAGE-BRIEF-STD).</summary>
public static class PackageBriefTemplatePptxFactory
{
    private sealed class ShapeIdCounter
    {
        public int Value = 2;
        public int Next() => Value++;
    }

    private sealed record SlideSpec(string Title, Action<StringBuilder, ShapeIdCounter> Build, string Transition = "fade");

    public static byte[] Create()
    {
        var slides = new[]
        {
            new SlideSpec("Kapak", BuildCoverSlide, "fade"),
            new SlideSpec("Yönetici Özeti", BuildSummarySlide, "push"),
            new SlideSpec("KPI Panosu", BuildKpiSlide, "split"),
            new SlideSpec("Tamamlanma", BuildFulfillmentSlide, "cover"),
            new SlideSpec("Sipariş Kalemleri", BuildLinesSlide, "fade"),
            new SlideSpec("Kalite", BuildQualitySlide, "wipe"),
            new SlideSpec("Lojistik & Kapanış", BuildClosingSlide, "push"),
        };

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes(slides.Length));
            WriteEntry(archive, "_rels/.rels", RootRelsXml);
            WriteEntry(archive, "ppt/presentation.xml", BuildPresentation(slides.Length));
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", BuildPresentationRels(slides.Length));
            WriteEntry(archive, "ppt/slideLayouts/slideLayout1.xml", SlideLayoutXml);
            WriteEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", SlideLayoutRelsXml);
            WriteEntry(archive, "ppt/slideMasters/slideMaster1.xml", SlideMasterXml);
            WriteEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", SlideMasterRelsXml);
            WriteEntry(archive, "ppt/theme/theme1.xml", ThemeXml);

            for (var i = 0; i < slides.Length; i++)
            {
                var n = i + 1;
                WriteEntry(archive, $"ppt/slides/slide{n}.xml", BuildSlide(slides[i]));
                WriteEntry(archive, $"ppt/slides/_rels/slide{n}.xml.rels", SlideRelsXml);
            }
        }

        return ms.ToArray();
    }

    private static void BuildCoverSlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(AccentBanner(id.Next(), 0, 0, 12192000, 1600200, "FF1F4E79"));
        shapes.Append(LogoPlaceholder(id.Next()));
        shapes.Append(TextShape(id.Next(), "{{documentName}}", 838200, 480000, 10515600, 900000, 4800, true, "FFFFFFFF"));
        shapes.Append(TextShape(id.Next(), "{{packageNo}}", 838200, 1800000, 10515600, 700000, 3200, true, "FF1F4E79"));
        shapes.Append(TextShape(id.Next(), "{{packageName}}", 838200, 2500000, 10515600, 900000, 2800, false, "FF44546A"));
        shapes.Append(TextShape(id.Next(), "{{customerName}}", 838200, 3600000, 10515600, 600000, 2400, false, "FF2F5496"));
        shapes.Append(TextShape(id.Next(), "Liste: {{issueDate}}", 838200, 5800000, 5200000, 400000, 1800, false, "FF7F7F7F"));
        shapes.Append(TextShape(id.Next(), "{{deliveryUrgencyLabel}}", 7200000, 5800000, 4200000, 400000, 1800, true, "FFED7D31"));
    }

    private static void BuildSummarySlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(TextShape(id.Next(), "İş Paketi Özeti", 838200, 457200, 10515600, 800000, 4000, true, "FF1F4E79"));
        shapes.Append(KpiCard(id.Next(), "Durum", "{{statusLabel}}", 838200, 1600000, 3200000, 1600000, "FF4472C4"));
        shapes.Append(KpiCard(id.Next(), "Termin", "{{deliveryDate}}", 4560000, 1600000, 3200000, 1600000, "FFED7D31"));
        shapes.Append(KpiCard(id.Next(), "Başlangıç", "{{beginDate}}", 8280000, 1600000, 3200000, 1600000, "FF70AD47"));
        shapes.Append(TextShape(id.Next(),
            "Sevkiyat durumu: {{shipmentSummary}} tamamlandı\n" +
            "Sevk edilen parça: {{shippedCount}} / {{partCount}}\n" +
            "Stok: {{stockCount}}",
            838200, 3600000, 10515600, 2200000, 2400, false, "FF44546A"));
    }

    private static void BuildKpiSlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(TextShape(id.Next(), "Operasyonel KPI'lar", 838200, 457200, 10515600, 800000, 4000, true, "FF1F4E79"));
        shapes.Append(KpiCard(id.Next(), "Kalem Sayısı", "{{lineCount}}", 838200, 1600000, 3200000, 1700000, "FF4472C4"));
        shapes.Append(KpiCard(id.Next(), "Sevkiyat", "{{shipmentSummary}}", 4560000, 1600000, 3200000, 1700000, "FFED7D31"));
        shapes.Append(KpiCard(id.Next(), "Tamamlanma", "{{fulfillmentPctLabel}}", 8280000, 1600000, 3200000, 1700000, "FF70AD47"));
        shapes.Append(KpiCard(id.Next(), "Açık NCR", "{{openNcrCount}}", 838200, 3600000, 3200000, 1700000, "FFC00000"));
        shapes.Append(KpiCard(id.Next(), "Açık CAPA", "{{openCapaCount}}", 4560000, 3600000, 3200000, 1700000, "FF7030A0"));
        shapes.Append(KpiCard(id.Next(), "Kalan Miktar", "{{remainingQuantity}}", 8280000, 3600000, 3200000, 1700000, "FF2F5496"));
    }

    private static void BuildFulfillmentSlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(TextShape(id.Next(), "Genel Tamamlanma", 838200, 457200, 10515600, 800000, 4000, true, "FF1F4E79"));
        shapes.Append(KpiCard(id.Next(), "Tamamlanma Oranı", "{{fulfillmentPctLabel}}", 838200, 1500000, 3200000, 1800000, "FF4472C4"));
        shapes.Append(FulfillmentBarChart(id, 4560000, 1500000, 6400000, 3200000));
        shapes.Append(TextShape(id.Next(),
            "Parça bazında: {{shippedCount}} / {{partCount}} sevk edildi",
            838200, 6100000, 10515600, 500000, 2200, false, "FF7F7F7F"));
    }

    private static string FulfillmentBarChart(ShapeIdCounter id, long originX, long originY, long width, long height)
    {
        var sb = new StringBuilder();
        var chartBottom = originY + height;
        var barWidth = width / 5;
        var gap = barWidth / 2;

        sb.Append(RectShape(id.Next(), originX, originY, width, height, "FFF8F9FA", "FFD0D5DD", 12700));
        sb.Append(LineShape(id.Next(), originX + 400000, chartBottom - 180000, originX + width - 200000, chartBottom - 180000, "FF44546A"));

        AppendBar(sb, id, "Sevk", "{{shippedCount}}", "FF70AD47", 0.82, originX + gap, chartBottom - 180000, barWidth);
        AppendBar(sb, id, "Kalan", "{{remainingQuantity}}", "FFED7D31", 0.55, originX + gap * 2 + barWidth, chartBottom - 180000, barWidth);
        AppendBar(sb, id, "Stok", "{{stockCount}}", "FF5B9BD5", 0.35, originX + gap * 3 + barWidth * 2, chartBottom - 180000, barWidth);

        sb.Append(TextShape(id.Next(), "Sevk / Kalan / Stok dağılımı", originX, originY - 180000, width, 400000, 2200, true, "FF2F5496"));
        return sb.ToString();
    }

    private static void AppendBar(
        StringBuilder sb,
        ShapeIdCounter id,
        string label,
        string value,
        string color,
        double heightRatio,
        long x,
        long chartBottom,
        long barWidth)
    {
        var barHeight = (long)(3200000 * heightRatio);
        var y = chartBottom - barHeight;
        sb.Append(RectShape(id.Next(), x, y, barWidth, barHeight, color, color, 0, $"FulfillBar {label}"));
        sb.Append(TextShape(id.Next(), label, x - 80000, chartBottom - 100000, barWidth + 160000, 350000, 1600, false, "FF44546A"));
        sb.Append(TextShape(id.Next(), value, x - 80000, y - 380000, barWidth + 160000, 350000, 1800, true, "FF1F4E79"));
    }

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

    private static void BuildLinesSlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(TextShape(id.Next(), "Sipariş Kalemleri — Özet", 838200, 457200, 10515600, 800000, 4000, true, "FF1F4E79"));
        shapes.Append(RectShape(id.Next(), 838200, 1500000, 10515600, 4800000, "FFF8F9FA", "FFD0D5DD", 12700));
        shapes.Append(TextShape(id.Next(), "{{linesSummary}}", 980000, 1650000, 10200000, 4500000, 2000, false, "FF44546A"));
    }

    private static void BuildQualitySlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(TextShape(id.Next(), "Kalite Durumu", 838200, 457200, 10515600, 800000, 4000, true, "FF1F4E79"));
        shapes.Append(KpiCard(id.Next(), "Açık NCR", "{{openNcrCount}}", 2200000, 2000000, 3600000, 2200000, "FFC00000"));
        shapes.Append(KpiCard(id.Next(), "Açık CAPA", "{{openCapaCount}}", 6400000, 2000000, 3600000, 2200000, "FF7030A0"));
        shapes.Append(TextShape(id.Next(),
            "Açık uygunsuzluk ve düzeltici faaliyetler operasyonel risk göstergesidir.\n" +
            "Detaylı kalite kayıtları için kontrol paneli XLSX dosyasına bakınız.",
            838200, 4800000, 10515600, 1400000, 2200, false, "FF44546A"));
    }

    private static void BuildClosingSlide(StringBuilder shapes, ShapeIdCounter id)
    {
        shapes.Append(AccentBanner(id.Next(), 0, 5800000, 12192000, 1058000, "FF1F4E79"));
        shapes.Append(TextShape(id.Next(), "Sevkiyat & Notlar", 838200, 457200, 10515600, 800000, 4000, true, "FF1F4E79"));
        shapes.Append(TextShape(id.Next(),
            "Sevkiyat adresi:\n{{deliveryAddress}}\n\nNotlar:\n{{notes}}",
            838200, 1500000, 10515600, 3200000, 2200, false, "FF44546A"));
        shapes.Append(TextShape(id.Next(), "Üretim: {{generatedAt}}", 838200, 6100000, 5200000, 500000, 2000, false, "FFFFFFFF"));
        shapes.Append(TextShape(id.Next(), "Teşekkürler", 7200000, 6000000, 4200000, 700000, 3600, true, "FFFFFFFF"));
    }

    private static string BuildSlide(SlideSpec slide)
    {
        var shapes = new StringBuilder();
        shapes.AppendLine("""              <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>""");
        shapes.AppendLine("""              <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>""");

        var shapeId = new ShapeIdCounter();
        slide.Build(shapes, shapeId);

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
            {BuildTransition(slide.Transition)}
            </p:sld>
            """;
    }

    private static string BuildTransition(string type) => type switch
    {
        "push" => """
              <p:transition spd="med" advClick="1"><p:push dir="l"/></p:transition>
            """,
        "split" => """
              <p:transition spd="med" advClick="1"><p:split orient="vert" dir="in"/></p:transition>
            """,
        "cover" => """
              <p:transition spd="med" advClick="1"><p:cover dir="lt"/></p:transition>
            """,
        "wipe" => """
              <p:transition spd="med" advClick="1"><p:blinds dim="vert"/></p:transition>
            """,
        _ => """
              <p:transition spd="med" advClick="1"><p:fade/></p:transition>
            """
    };

    private static string AccentBanner(int id, long x, long y, long cx, long cy, string fill) =>
        RectShape(id, x, y, cx, cy, fill, fill, 0);

    private static string KpiCard(int id, string label, string value, long x, long y, long cx, long cy, string accent) =>
        $"""
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
                            <a:r><a:rPr lang="tr-TR" sz="1800" b="1"><a:solidFill><a:srgbClr val="{accent}"/></a:solidFill></a:rPr><a:t>{Esc(label)}</a:t></a:r>
                          </a:p>
                          <a:p>
                            <a:pPr algn="ctr"/>
                            <a:r><a:rPr lang="tr-TR" sz="3200" b="1"><a:solidFill><a:srgbClr val="FF1F4E79"/></a:solidFill></a:rPr><a:t>{Esc(value)}</a:t></a:r>
                          </a:p>
                        </p:txBody>
                      </p:sp>
            """;

    private static string LogoPlaceholder(int id) =>
        $"""
                      <p:pic>
                        <p:nvPicPr>
                          <p:cNvPr id="{id}" name="Domain Logo"/>
                          <p:cNvPicPr/>
                          <p:nvPr/>
                        </p:nvPicPr>
                        <p:blipFill>
                          <a:stretch><a:fillRect/></a:stretch>
                        </p:blipFill>
                        <p:spPr>
                          <a:xfrm><a:off x="9000000" y="200000"/><a:ext cx="2800000" cy="1200000"/></a:xfrm>
                          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                        </p:spPr>
                      </p:pic>
            """;

    private static string RectShape(int id, long x, long y, long cx, long cy, string fill, string line, int lineWidth, string? shapeName = null)
    {
        var name = shapeName ?? $"Shape {id}";
        return $"""
                      <p:sp>
                        <p:nvSpPr><p:cNvPr id="{id}" name="{Esc(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
                        <p:spPr>
                          <a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{cx}" cy="{cy}"/></a:xfrm>
                          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                          <a:solidFill><a:srgbClr val="{fill}"/></a:solidFill>
                          <a:ln w="{lineWidth}"><a:solidFill><a:srgbClr val="{line}"/></a:solidFill></a:ln>
                        </p:spPr>
                        <p:txBody><a:bodyPr/><a:lstStyle/></p:txBody>
                      </p:sp>
            """;
    }

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

    private static string Esc(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string BuildContentTypes(int slideCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        sb.AppendLine("""  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        sb.AppendLine("""  <Default Extension="xml" ContentType="application/xml"/>""");
        sb.AppendLine("""  <Default Extension="png" ContentType="image/png"/>""");
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

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private const string RootRelsXml =
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>
        </Relationships>
        """;

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
          <p:cSld name="Blank"><p:spTree>
            <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
            <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
          </p:spTree></p:cSld>
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
          <p:cSld><p:bg><p:bgRef idx="1001"><a:schemeClr val="bg1"/></p:bgRef></p:bg>
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
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Odak Executive">
          <a:themeElements>
            <a:clrScheme name="Odak">
              <a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1>
              <a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>
              <a:dk2><a:srgbClr val="1F4E79"/></a:dk2>
              <a:lt2><a:srgbClr val="F2F2F2"/></a:lt2>
              <a:accent1><a:srgbClr val="4472C4"/></a:accent1>
              <a:accent2><a:srgbClr val="ED7D31"/></a:accent2>
              <a:accent3><a:srgbClr val="70AD47"/></a:accent3>
              <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
              <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
              <a:accent6><a:srgbClr val="7030A0"/></a:accent6>
              <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
              <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="Office">
              <a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
              <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
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
}
