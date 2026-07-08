using System.IO.Compression;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Models;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Ensures XLSX generation uses a valid spreadsheet template (not a mis-uploaded DOCX).
/// </summary>
internal static class XlsxTemplateBytesResolver
{
    public static byte[] Resolve(byte[] downloadedBytes, DmDocumentTemplate template)
    {
        if (IsXlsxZip(downloadedBytes))
            return downloadedBytes;

        var code = template.code?.Trim() ?? string.Empty;
        if (string.Equals(code, "SHIPMENT-LIST-STD", StringComparison.OrdinalIgnoreCase))
            return ShipmentListTemplateXlsxFactory.Create();

        var fileName = template.sourceFileName?.Trim() ?? "(unknown)";
        throw DocumentException.Validation(
            "TEMPLATE_SOURCE_NOT_XLSX",
            $"Template source '{fileName}' is not a valid XLSX workbook. Re-upload an .xlsx template.",
            $"Şablon kaynağı '{fileName}' geçerli bir XLSX dosyası değil. Lütfen .xlsx şablon yükleyin.");
    }

    internal static bool IsXlsxZip(byte[] bytes)
    {
        if (bytes.Length < 4)
            return false;

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            return archive.Entries.Any(e =>
                string.Equals(e.FullName, "xl/workbook.xml", StringComparison.OrdinalIgnoreCase)
                || e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
