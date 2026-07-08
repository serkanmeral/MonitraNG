using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Doughnut grafiklerin strCache/numCache noktalarını doldurur — Excel/Collabora boş cache ile dilim çizmez.
/// </summary>
public static class XlsxDonutChartCachePatcher
{
    private static readonly XNamespace C =
        "http://schemas.openxmlformats.org/drawingml/2006/chart";

    public static byte[] Apply(
        byte[] xlsxBytes,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> donutRows)
    {
        if (donutRows.Count == 0)
            return xlsxBytes;

        var categories = new List<string>();
        var values = new List<double>();
        foreach (var row in donutRows)
        {
            categories.Add(ReadString(row, "category") ?? string.Empty);
            values.Add(ReadDouble(row, "amount"));
        }

        if (categories.Count == 0)
            return xlsxBytes;

        using var input = new MemoryStream(xlsxBytes, writable: false);
        using var output = new MemoryStream();
        var changed = false;

        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
            {
                var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream = entry.Open();
                using var outStream = newEntry.Open();

                if (entry.FullName.StartsWith("xl/charts/", StringComparison.OrdinalIgnoreCase)
                    && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(inStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                    var xml = reader.ReadToEnd();
                    if (xml.Contains("doughnutChart", StringComparison.OrdinalIgnoreCase))
                    {
                        var patched = PatchDoughnutChartXml(xml, categories, values);
                        if (!string.Equals(patched, xml, StringComparison.Ordinal))
                            changed = true;
                        WriteUtf8(outStream, patched);
                    }
                    else
                    {
                        WriteUtf8(outStream, xml);
                    }

                    continue;
                }

                inStream.CopyTo(outStream);
            }
        }

        return changed ? output.ToArray() : xlsxBytes;
    }

    private static void WriteUtf8(Stream stream, string xml)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string PatchDoughnutChartXml(
        string xml,
        IReadOnlyList<string> categories,
        IReadOnlyList<double> values)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var doughnut = doc.Descendants(C + "doughnutChart").FirstOrDefault();
            if (doughnut is null)
                return xml;

            var ser = doughnut.Elements(C + "ser").FirstOrDefault();
            if (ser is null)
                return xml;

            var cat = ser.Element(C + "cat")?.Element(C + "strRef");
            if (cat is not null)
            {
                cat.Element(C + "strCache")?.Remove();
                cat.Add(BuildStringCache(categories));
            }

            var val = ser.Element(C + "val")?.Element(C + "numRef");
            if (val is not null)
            {
                val.Element(C + "numCache")?.Remove();
                val.Add(BuildNumberCache(values));
            }

            return SerializeUtf8(doc);
        }
        catch
        {
            return PatchDoughnutChartXmlRegex(xml, categories, values);
        }
    }

    private static string SerializeUtf8(XDocument doc)
    {
        using var ms = new MemoryStream();
        using var writer = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            Indent = false,
            CloseOutput = false
        });
        doc.Save(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static XElement BuildStringCache(IReadOnlyList<string> categories)
    {
        var cache = new XElement(C + "strCache",
            new XElement(C + "ptCount", new XAttribute("val", categories.Count)));

        for (var i = 0; i < categories.Count; i++)
        {
            cache.Add(new XElement(C + "pt",
                new XAttribute("idx", i),
                new XElement(C + "v", categories[i])));
        }

        return cache;
    }

    private static XElement BuildNumberCache(IReadOnlyList<double> values)
    {
        var cache = new XElement(C + "numCache",
            new XElement(C + "formatCode", "General"),
            new XElement(C + "ptCount", new XAttribute("val", values.Count)));

        for (var i = 0; i < values.Count; i++)
        {
            cache.Add(new XElement(C + "pt",
                new XAttribute("idx", i),
                new XElement(C + "v", values[i].ToString(CultureInfo.InvariantCulture))));
        }

        return cache;
    }

    private static string PatchDoughnutChartXmlRegex(
        string xml,
        IReadOnlyList<string> categories,
        IReadOnlyList<double> values)
    {
        var catCache = BuildStringCacheXml(categories);
        var numCache = BuildNumberCacheXml(values);

        xml = Regex.Replace(
            xml,
            @"(<c:cat>\s*<c:strRef>.*?<c:strCache>)(.*?)(</c:strCache>)",
            m => m.Groups[1].Value + catCache + m.Groups[3].Value,
            RegexOptions.Singleline);

        xml = Regex.Replace(
            xml,
            @"(<c:val>\s*<c:numRef>.*?<c:numCache>)(.*?)(</c:numCache>)",
            m => m.Groups[1].Value + numCache + m.Groups[3].Value,
            RegexOptions.Singleline);

        return xml;
    }

    private static string BuildStringCacheXml(IReadOnlyList<string> categories)
    {
        var sb = new StringBuilder();
        sb.Append("<c:strCache>");
        sb.Append(CultureInfo.InvariantCulture, $"<c:ptCount val=\"{categories.Count}\"/>");
        for (var i = 0; i < categories.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<c:pt idx=\"{i}\"><c:v>{EscapeXml(categories[i])}</c:v></c:pt>");
        }

        sb.Append("</c:strCache>");
        return sb.ToString();
    }

    private static string BuildNumberCacheXml(IReadOnlyList<double> values)
    {
        var sb = new StringBuilder();
        sb.Append("<c:numCache><c:formatCode>General</c:formatCode>");
        sb.Append(CultureInfo.InvariantCulture, $"<c:ptCount val=\"{values.Count}\"/>");
        for (var i = 0; i < values.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"<c:pt idx=\"{i}\"><c:v>{values[i].ToString(CultureInfo.InvariantCulture)}</c:v></c:pt>");
        }

        sb.Append("</c:numCache>");
        return sb.ToString();
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (row.TryGetValue(key, out var raw) && raw is not null)
            return raw.ToString()?.Trim();

        foreach (var kv in row)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value?.ToString()?.Trim();
        }

        return null;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!TryRead(row, key, out var raw) || raw is null)
            return 0;

        return raw switch
        {
            double d => d,
            float f => f,
            decimal m => (double)m,
            int or long or short or byte => Convert.ToDouble(raw, CultureInfo.InvariantCulture),
            _ => double.TryParse(raw.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0
        };
    }

    private static bool TryRead(IReadOnlyDictionary<string, object?> row, string key, out object? value)
    {
        if (row.TryGetValue(key, out value))
            return true;

        foreach (var kv in row)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
