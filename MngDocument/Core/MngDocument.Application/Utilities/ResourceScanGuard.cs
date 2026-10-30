using MngDocument.Application.Exceptions;
using MngDocument.Domain.Constants;

namespace MngDocument.Application.Utilities;

public static class ResourceScanGuard
{
    public static void EnsureAllowsContent(string? type, string? origin, string? scanStatus)
    {
        if (ResourceScanStatus.AllowsContent(type, origin, scanStatus))
            return;

        var status = scanStatus?.Trim() ?? string.Empty;
        if (string.Equals(status, ResourceScanStatus.Scanning, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "SCAN_PENDING",
                "The file is still being scanned.",
                "Dosya taranıyor. Tarama bitmeden açılamaz.");
        }

        if (string.Equals(status, ResourceScanStatus.Threat, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "SCAN_THREAT",
                "The file matched a malware signature.",
                "Dosyada tehdit bulundu. İndirme ve önizleme kapalı.");
        }

        throw DocumentException.Validation(
            "SCAN_FAILED",
            "The file could not be scanned.",
            "Dosya taranamadı. Temiz sayılmaz.");
    }
}
