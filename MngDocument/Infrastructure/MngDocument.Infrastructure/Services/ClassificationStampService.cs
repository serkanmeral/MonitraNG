using System.IO.Packaging;
using System.Text;
using System.Xml.Linq;
using MngDocument.Application.Contracts.Dlp;
using MngDocument.Application.Interfaces;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Office: OPC custom.xml. PDF: comment before %%EOF (`% MngDlp:ver|id|name|sens`).
/// Unknown types are returned unchanged.
/// </summary>
public sealed class ClassificationStampService : IClassificationStampService
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
    private static readonly XNamespace Vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
    private const string CustomRelType =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties";
    private const string CustomContentType =
        "application/vnd.openxmlformats-officedocument.custom-properties+xml";

    public byte[] Apply(byte[] content, string? extensionOrFileName, ClassificationStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(stamp);

        var ext = NormalizeExt(extensionOrFileName, content);
        try
        {
            return ext switch
            {
                ".docx" or ".xlsx" or ".pptx" => ApplyOffice(content, stamp),
                ".pdf" => ApplyPdf(content, stamp),
                _ => content
            };
        }
        catch
        {
            return content;
        }
    }

    public ClassificationStamp? TryRead(byte[] content, string? extensionOrFileName)
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

    private static byte[] ApplyOffice(byte[] content, ClassificationStamp stamp)
    {
        using var stream = new MemoryStream();
        stream.Write(content);
        stream.Position = 0;

        using (var package = Package.Open(stream, FileMode.Open, FileAccess.ReadWrite))
        {
            var partUri = new Uri("/docProps/custom.xml", UriKind.Relative);
            PackagePart part;
            if (package.PartExists(partUri))
            {
                part = package.GetPart(partUri);
            }
            else
            {
                part = package.CreatePart(partUri, CustomContentType, CompressionOption.Normal);
                package.CreateRelationship(partUri, TargetMode.Internal, CustomRelType);
            }

            var props = LoadCustomProperties(part);
            UpsertString(props, ClassificationStampKeys.Id, stamp.ClassificationId);
            UpsertString(props, ClassificationStampKeys.Name, stamp.ClassificationName);
            UpsertString(props, ClassificationStampKeys.Sensitivity, stamp.Sensitivity.ToString());
            UpsertString(props, ClassificationStampKeys.Version, stamp.SchemaVersion.ToString());
            SaveCustomProperties(part, props);
            package.Flush();
        }

        return stream.ToArray();
    }

    private static ClassificationStamp? ReadOffice(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var package = Package.Open(stream, FileMode.Open, FileAccess.Read);
        var partUri = new Uri("/docProps/custom.xml", UriKind.Relative);
        if (!package.PartExists(partUri))
            return null;

        var props = LoadCustomProperties(package.GetPart(partUri));
        var id = ReadString(props, ClassificationStampKeys.Id);
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var name = ReadString(props, ClassificationStampKeys.Name) ?? string.Empty;
        _ = int.TryParse(ReadString(props, ClassificationStampKeys.Sensitivity), out var sensitivity);
        _ = int.TryParse(ReadString(props, ClassificationStampKeys.Version), out var version);
        if (version <= 0)
            version = ClassificationStampKeys.SchemaVersion;

        return new ClassificationStamp(id, name, sensitivity, version);
    }

    private static XElement LoadCustomProperties(PackagePart part)
    {
        using var s = part.GetStream(FileMode.Open, FileAccess.Read);
        if (s.Length == 0)
            return NewPropertiesRoot();
        var doc = XDocument.Load(s);
        return doc.Root ?? NewPropertiesRoot();
    }

    private static void SaveCustomProperties(PackagePart part, XElement properties)
    {
        using var s = part.GetStream(FileMode.Create, FileAccess.Write);
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), properties).Save(s);
    }

    private static XElement NewPropertiesRoot() =>
        new(Ns + "Properties",
            new XAttribute(XNamespace.Xmlns + "vt", Vt),
            new XAttribute("xmlns", Ns.NamespaceName));

    private static void UpsertString(XElement properties, string name, string value)
    {
        foreach (var el in properties.Elements(Ns + "property").ToList())
        {
            if (string.Equals((string?)el.Attribute("name"), name, StringComparison.Ordinal))
                el.Remove();
        }

        var nextPid = 2;
        foreach (var el in properties.Elements(Ns + "property"))
        {
            if (int.TryParse((string?)el.Attribute("pid"), out var pid) && pid >= nextPid)
                nextPid = pid + 1;
        }

        properties.Add(new XElement(
            Ns + "property",
            new XAttribute("fmtid", "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"),
            new XAttribute("pid", nextPid),
            new XAttribute("name", name),
            new XElement(Vt + "lpwstr", value)));
    }

    private static string? ReadString(XElement properties, string name)
    {
        var el = properties.Elements(Ns + "property")
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("name"), name, StringComparison.Ordinal));
        return el?.Elements().FirstOrDefault()?.Value;
    }

    private static byte[] ApplyPdf(byte[] content, ClassificationStamp stamp)
    {
        var keywords =
            $"{ClassificationStampKeys.PdfKeywordsPrefix}{stamp.SchemaVersion}|{stamp.ClassificationId}|{stamp.ClassificationName}|{stamp.Sensitivity}";
        return AppendPdfKeywordsComment(content, keywords);
    }

    private static ClassificationStamp? ReadPdf(byte[] content)
    {
        var text = Encoding.Latin1.GetString(content);
        var marker = ClassificationStampKeys.PdfKeywordsPrefix;
        var idx = text.LastIndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
            return null;

        var slice = text.Substring(idx);
        var end = slice.IndexOfAny(['\r', '\n', ')', '>']);
        if (end > 0)
            slice = slice[..end];

        var parts = slice[marker.Length..].Split('|');
        if (parts.Length < 4)
            return null;
        _ = int.TryParse(parts[0], out var version);
        _ = int.TryParse(parts[3], out var sensitivity);
        return new ClassificationStamp(parts[1], parts[2], sensitivity, version <= 0 ? 1 : version);
    }

    private static byte[] AppendPdfKeywordsComment(byte[] content, string keywords)
    {
        var comment = Encoding.ASCII.GetBytes($"\n% {keywords}\n");
        var eof = Encoding.ASCII.GetBytes("%%EOF");
        var lastEof = LastIndexOf(content, eof);
        if (lastEof < 0)
        {
            var copy = new byte[content.Length + comment.Length];
            Buffer.BlockCopy(content, 0, copy, 0, content.Length);
            Buffer.BlockCopy(comment, 0, copy, content.Length, comment.Length);
            return copy;
        }

        var result = new byte[content.Length + comment.Length];
        Buffer.BlockCopy(content, 0, result, 0, lastEof);
        Buffer.BlockCopy(comment, 0, result, lastEof, comment.Length);
        Buffer.BlockCopy(content, lastEof, result, lastEof + comment.Length, content.Length - lastEof);
        return result;
    }

    private static int LastIndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = haystack.Length - needle.Length; i >= 0; i--)
        {
            var ok = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
                return i;
        }
        return -1;
    }
}
