namespace MngDocument.Infrastructure.Helpers;

internal static class TemplateFileNameHelper
{
    private const string DefaultExtension = ".docx";
    private const string FallbackBaseName = "belge";

    public static string ResolveDisplayFileName(string? name, string? code, string? sourceFileName)
    {
        var baseName = FirstNonEmpty(
            name?.Trim(),
            code?.Trim(),
            Path.GetFileNameWithoutExtension(sourceFileName?.Trim()));

        var ext = Path.GetExtension(sourceFileName?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(ext)
            || !IsOfficeTemplateExtension(ext))
        {
            ext = DefaultExtension;
        }

        return SanitizeBaseName(baseName) + ext.ToLowerInvariant();
    }

    private static bool IsOfficeTemplateExtension(string ext) =>
        ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".xlsm", StringComparison.OrdinalIgnoreCase)
        || ext.Equals(".pptx", StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string SanitizeBaseName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return FallbackBaseName;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
            sb.Append(invalid.Contains(c) ? '_' : c);

        var cleaned = sb.ToString().Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(cleaned) ? FallbackBaseName : cleaned;
    }
}
