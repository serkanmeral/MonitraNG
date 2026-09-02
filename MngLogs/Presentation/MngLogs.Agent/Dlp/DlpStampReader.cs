using System.IO.Packaging;
using System.Text;
using System.Xml.Linq;

namespace MngLogs.Agent.Dlp;

/// <summary>Reads Dilim 0 stamps: Office custom.xml <c>MngDlp.*</c> and PDF <c>% MngDlp:</c> comments.</summary>
public static class DlpStampReader
{
    public const string IdKey = "MngDlp.ClassificationId";
    public const string NameKey = "MngDlp.ClassificationName";
    public const string SensitivityKey = "MngDlp.Sensitivity";
    public const string PdfPrefix = "MngDlp:";

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

    public static DlpClassificationHit? TryReadFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            return TryRead(bytes, path);
        }
        catch
        {
            return null;
        }
    }

    public static DlpClassificationHit? TryRead(byte[] content, string? extensionOrFileName)
    {
        if (content is null || content.Length == 0)
            return null;
        var ext = NormalizeExt(extensionOrFileName, content);
        try
        {
            return ext switch
            {
                ".docx" or ".xlsx" or ".pptx" => ReadOffice(content),
                ".pdf" => ReadPdf(content),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeExt(string? extensionOrFileName, byte[] content)
    {
        var raw = extensionOrFileName?.Trim() ?? string.Empty;
        if (raw.Contains('.', StringComparison.Ordinal) && !raw.StartsWith('.'))
            raw = Path.GetExtension(raw);
        if (!raw.StartsWith('.') && raw.Length > 0)
            raw = "." + raw;
        var ext = raw.ToLowerInvariant();
        if (ext is ".docx" or ".xlsx" or ".pptx" or ".pdf")
            return ext;
        if (content.Length >= 5 && content[0] == (byte)'%' && content[1] == (byte)'P' && content[2] == (byte)'D')
            return ".pdf";
        if (content.Length >= 2 && content[0] == (byte)'P' && content[1] == (byte)'K')
            return ".docx";
        return ext;
    }

    private static DlpClassificationHit? ReadOffice(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var package = Package.Open(stream, FileMode.Open, FileAccess.Read);
        var partUri = new Uri("/docProps/custom.xml", UriKind.Relative);
        if (!package.PartExists(partUri))
            return null;
        using var s = package.GetPart(partUri).GetStream(FileMode.Open, FileAccess.Read);
        if (s.Length == 0)
            return null;
        var root = XDocument.Load(s).Root;
        if (root is null)
            return null;
        var id = ReadProp(root, IdKey);
        if (string.IsNullOrWhiteSpace(id))
            return null;
        _ = int.TryParse(ReadProp(root, SensitivityKey), out var sensitivity);
        return new DlpClassificationHit
        {
            Id = id,
            Name = ReadProp(root, NameKey),
            Sensitivity = sensitivity,
            Source = "embedded"
        };
    }

    private static string? ReadProp(XElement properties, string name) =>
        properties.Elements(Ns + "property")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("name"), name, StringComparison.Ordinal))
            ?.Elements()
            .FirstOrDefault()
            ?.Value;

    private static DlpClassificationHit? ReadPdf(byte[] content)
    {
        var text = Encoding.Latin1.GetString(content);
        var idx = text.LastIndexOf(PdfPrefix, StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var slice = text[idx..];
        var end = slice.IndexOfAny(['\r', '\n', ')', '>']);
        if (end > 0)
            slice = slice[..end];
        var parts = slice[PdfPrefix.Length..].Split('|');
        if (parts.Length < 4)
            return null;
        _ = int.TryParse(parts[3], out var sensitivity);
        return new DlpClassificationHit
        {
            Id = parts[1],
            Name = parts[2],
            Sensitivity = sensitivity,
            Source = "embedded"
        };
    }
}
