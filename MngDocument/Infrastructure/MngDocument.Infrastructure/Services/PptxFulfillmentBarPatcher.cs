using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MngDocument.Infrastructure.Services;

/// <summary>Tamamlanma slaydındaki çubuk yüksekliklerini sevk/kalan/stok değerlerine göre günceller.</summary>
public static class PptxFulfillmentBarPatcher
{
    private const string FulfillmentSlidePath = "ppt/slides/slide4.xml";
    private const long MaxBarHeight = 2624000;
    private const long ChartBaseline = 4520000;
    private const double MinRatio = 0.08;

    private static readonly (string ShapeName, string ValueKey)[] BarMappings =
    [
        ("FulfillBar Sevk", "shippedCount"),
        ("FulfillBar Kalan", "remainingQuantity"),
        ("FulfillBar Stok", "stockCount"),
    ];

    public static byte[] Apply(byte[] pptxBytes, IReadOnlyDictionary<string, string> values)
    {
        var shipped = ReadDouble(values, "shippedCount");
        var remaining = ReadDouble(values, "remainingQuantity");
        var stock = ReadDouble(values, "stockCount");
        var max = Math.Max(1, Math.Max(shipped, Math.Max(remaining, stock)));

        try
        {
            using var input = new MemoryStream(pptxBytes, writable: false);
            using var output = new MemoryStream();

            using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
            using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in readArchive.Entries)
                {
                    var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using var inStream = entry.Open();
                    using var outStream = newEntry.Open();

                    if (string.Equals(entry.FullName, FulfillmentSlidePath, StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(inStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                        var xml = reader.ReadToEnd();
                        foreach (var (shapeName, valueKey) in BarMappings)
                        {
                            var amount = ReadDouble(values, valueKey);
                            var ratio = Math.Max(MinRatio, amount / max);
                            var barHeight = (long)(MaxBarHeight * ratio);
                            var y = ChartBaseline - barHeight;
                            xml = PatchBarShape(xml, shapeName, y, barHeight);
                        }

                        WriteUtf8(outStream, xml);
                    }
                    else
                    {
                        inStream.CopyTo(outStream);
                    }
                }
            }

            return output.ToArray();
        }
        catch
        {
            return pptxBytes;
        }
    }

    private static string PatchBarShape(string xml, string shapeName, long y, long barHeight)
    {
        var escapedName = Regex.Escape(shapeName);
        var pattern = new Regex(
            $@"(<p:cNvPr[^>]*name=""{escapedName}""[^>]*/>\s*</p:nvSpPr>\s*<p:spPr>\s*<a:xfrm>\s*<a:off x=""(\d+)"" y="")(\d+)(""[^>]*/>\s*<a:ext cx=""(\d+)"" cy="")(\d+)(""[^>]*/>)",
            RegexOptions.Singleline);

        return pattern.Replace(xml, m =>
            m.Groups[1].Value + y + m.Groups[3].Value + m.Groups[4].Value + barHeight + m.Groups[6].Value);
    }

    private static void WriteUtf8(Stream stream, string xml)
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!TryRead(values, key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return 0;

        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static bool TryRead(IReadOnlyDictionary<string, string> values, string key, out string? value)
    {
        if (values.TryGetValue(key, out value))
            return true;

        foreach (var kv in values)
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
