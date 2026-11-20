using System.Text;

namespace MngExtensions.Domain;

/// <summary>
/// A door photo is a JPEG carried with the passage. It does not identify the person.
/// </summary>
public static class PassageFrame
{
    public const int MaxBytes = 800_000;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    public const string Live = "live";
    public const string Done = "done";

    public static string? NormalizeJpeg(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var marker = text.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
            text = text[(marker + "base64,".Length)..];
        text = text.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        if (text.Length < 32 || text.Length > MaxBytes * 2)
            return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length < 32 || bytes.Length > MaxBytes)
            return null;
        if (bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
            return null;
        return Convert.ToBase64String(bytes);
    }
}
