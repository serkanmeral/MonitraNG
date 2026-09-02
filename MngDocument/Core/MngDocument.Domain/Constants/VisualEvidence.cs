namespace MngDocument.Domain.Constants;

/// <summary>
/// F1-3 görsel kanıt: PNG/SVG/draw.io vb. uzantı tanıma. Çizim editörü yoktur.
/// </summary>
public static class VisualEvidence
{
    public const string DrawioExtension = "drawio";
    public const string DrawioMime = "application/vnd.jgraph.mxfile";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "avif", "ico"
    };

    public static string NormalizeExtension(string? extension, string? fileName)
    {
        var name = fileName?.Trim() ?? string.Empty;
        if (name.EndsWith(".drawio.xml", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            return DrawioExtension;
        }

        var ext = (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        if (!string.IsNullOrEmpty(ext)) return ext;

        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..].ToLowerInvariant() : string.Empty;
    }

    public static bool IsDrawio(string? extension, string? fileName, string? mime)
    {
        var name = fileName ?? string.Empty;
        if (name.EndsWith(".drawio.xml", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(NormalizeExtension(extension, fileName), DrawioExtension, StringComparison.OrdinalIgnoreCase))
            return true;

        var m = (mime ?? string.Empty).ToLowerInvariant();
        return m.Contains("jgraph", StringComparison.Ordinal) || m.Contains("mxfile", StringComparison.Ordinal);
    }

    public static bool IsVisualImage(string? extension, string? fileName, string? mime)
    {
        var ext = NormalizeExtension(extension, fileName);
        if (ImageExtensions.Contains(ext)) return true;
        var m = (mime ?? string.Empty).ToLowerInvariant();
        return m.StartsWith("image/", StringComparison.Ordinal);
    }

    public static bool IsPdf(string? extension, string? fileName, string? mime)
    {
        var ext = NormalizeExtension(extension, fileName);
        if (string.Equals(ext, "pdf", StringComparison.OrdinalIgnoreCase)) return true;
        var m = (mime ?? string.Empty).ToLowerInvariant();
        return m.Contains("pdf", StringComparison.Ordinal);
    }

    /// <summary>Görsel kanıt (diyagram türü otomatik atama): görüntü + draw.io.</summary>
    public static bool IsVisualEvidence(string? extension, string? fileName, string? mime) =>
        IsDrawio(extension, fileName, mime) || IsVisualImage(extension, fileName, mime);

    /// <summary>Yüklemede sürüm snapshot yazılacak yüklemeler (görsel + PDF).</summary>
    public static bool ShouldSnapshotOnCreate(string? extension, string? fileName, string? mime) =>
        IsVisualEvidence(extension, fileName, mime) || IsPdf(extension, fileName, mime);

    public static string GuessContentType(string? extension, string? fileName, string? mime)
    {
        if (!string.IsNullOrWhiteSpace(mime)
            && !string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return mime.Trim();
        }

        var ext = NormalizeExtension(extension, fileName);
        return ext switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            "svg" => "image/svg+xml",
            "avif" => "image/avif",
            "ico" => "image/x-icon",
            "pdf" => "application/pdf",
            "drawio" => DrawioMime,
            "xml" => "application/xml",
            _ => string.IsNullOrWhiteSpace(mime) ? "application/octet-stream" : mime.Trim()
        };
    }
}
