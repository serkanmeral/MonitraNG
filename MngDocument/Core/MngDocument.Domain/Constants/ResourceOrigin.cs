namespace MngDocument.Domain.Constants;

/// <summary>
/// <c>dm_resources.origin</c> değerleri — kaynak dosyanın kökeni / editör davranışı.
/// </summary>
public static class ResourceOrigin
{
    public const string Upload = "upload";
    public const string Native = "native";
    public const string Manual = "manual";
    public const string System = "system";

    /// <summary>Collabora editöründe açılabilen yönetilen dökümanlar.</summary>
    public static bool IsManagedDocument(string? origin)
    {
        var normalized = origin?.Trim();
        if (string.IsNullOrEmpty(normalized)) return false;
        return string.Equals(normalized, Native, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, Manual, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, System, StringComparison.OrdinalIgnoreCase);
    }
}
