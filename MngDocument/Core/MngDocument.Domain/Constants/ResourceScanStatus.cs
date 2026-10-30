namespace MngDocument.Domain.Constants;

/// <summary>
/// Yüklenen dosyanın antivirüs durumu. Boş veya <c>none</c> bu özellikten önceki kayıttır ve erişime açıktır.
/// </summary>
public static class ResourceScanStatus
{
    public const string Scanning = "scanning";
    public const string Clean = "clean";
    public const string Threat = "threat";
    public const string Failed = "failed";

    public static bool IsUploadFile(string? type, string? origin) =>
        string.Equals(type, ResourceType.File, StringComparison.OrdinalIgnoreCase)
        && string.Equals(origin?.Trim(), ResourceOrigin.Upload, StringComparison.OrdinalIgnoreCase);

    public static bool AllowsContent(string? type, string? origin, string? scanStatus)
    {
        if (!IsUploadFile(type, origin))
            return true;

        if (string.IsNullOrWhiteSpace(scanStatus)
            || string.Equals(scanStatus.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(scanStatus.Trim(), Clean, StringComparison.OrdinalIgnoreCase);
    }
}
