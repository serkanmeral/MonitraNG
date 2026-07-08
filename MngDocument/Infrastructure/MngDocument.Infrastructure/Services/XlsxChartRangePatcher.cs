using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Gömülü XLSX grafik serilerinin Veri sayfası aralığını gerçek satır sayısına göre daraltır (Yol A).
/// </summary>
public static class XlsxChartRangePatcher
{
    private static readonly Regex VeriRangeRegex = new(
        @"Veri!\$([A-Z]+)\$(\d+):\$\1\$(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static byte[] ClampEmbeddedChartRanges(byte[] xlsxBytes, int lastDataRow)
    {
        if (lastDataRow < 2)
            lastDataRow = 2;

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
                    using var reader = new StreamReader(inStream, Encoding.UTF8, leaveOpen: true);
                    var xml = reader.ReadToEnd();
                    var patched = VeriRangeRegex.Replace(xml, match =>
                    {
                        var col = match.Groups[1].Value;
                        var startRow = int.Parse(match.Groups[2].Value);
                        var cap = int.Parse(match.Groups[3].Value);

                        // Bar chart: Veri!A10:C* — donut E2:F4 sabit, dokunma.
                        if (startRow < 10 || col is not ("A" or "B" or "C"))
                            return match.Value;

                        if (cap <= lastDataRow)
                            return match.Value;

                        changed = true;
                        return $"Veri!${col}${startRow}:${col}${lastDataRow}";
                    });

                    var bytes = Encoding.UTF8.GetBytes(patched);
                    outStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    inStream.CopyTo(outStream);
                }
            }
        }

        return changed ? output.ToArray() : xlsxBytes;
    }
}
