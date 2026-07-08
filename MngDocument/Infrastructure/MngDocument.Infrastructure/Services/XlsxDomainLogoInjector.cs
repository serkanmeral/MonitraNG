using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>Domain logosunu Kontrol Paneli XLSX üst bandına gömer (A1:C3).</summary>
public static class XlsxDomainLogoInjector
{
    private const string DrawingPath = "xl/drawings/drawing1.xml";
    private const string DrawingRelsPath = "xl/drawings/_rels/drawing1.xml.rels";
    private const string ContentTypesPath = "[Content_Types].xml";
    private const string MediaPath = "xl/media/domain-logo.png";

    public static byte[] TryInject(byte[] xlsxBytes, byte[] logoBytes, string extension)
    {
        if (logoBytes is not { Length: > 0 })
            return xlsxBytes;

        var mediaExt = NormalizeExtension(extension);
        var mediaPath = mediaExt.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || mediaExt.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? "xl/media/domain-logo.jpg"
            : MediaPath;

        try
        {
            using var input = new MemoryStream(xlsxBytes, writable: false);
            using var output = new MemoryStream();

            string? drawingXml = null;
            string? drawingRelsXml = null;
            string? contentTypesXml = null;

            using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
            {
                drawingXml = ReadTextEntry(readArchive, DrawingPath);
                drawingRelsXml = ReadTextEntry(readArchive, DrawingRelsPath);
                contentTypesXml = ReadTextEntry(readArchive, ContentTypesPath);

                if (drawingXml is null || drawingRelsXml is null || contentTypesXml is null)
                    return xlsxBytes;

                using var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var entry in readArchive.Entries)
                {
                    if (string.Equals(entry.FullName, DrawingPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.FullName, DrawingRelsPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.FullName, ContentTypesPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.FullName, mediaPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    CopyEntry(entry, writeArchive);
                }

                WriteBytesEntry(writeArchive, mediaPath, logoBytes);
                WriteTextEntry(writeArchive, DrawingPath, InjectPictureAnchor(drawingXml, mediaPath, drawingRelsXml, out var patchedRels));
                WriteTextEntry(writeArchive, DrawingRelsPath, patchedRels);
                WriteTextEntry(writeArchive, ContentTypesPath, PatchContentTypes(contentTypesXml, mediaPath));
            }

            return output.ToArray();
        }
        catch
        {
            return xlsxBytes;
        }
    }

    private static string InjectPictureAnchor(string drawingXml, string mediaPath, string relsXml, out string patchedRels)
    {
        var imageRelId = "rIdLogo";
        patchedRels = InjectImageRelationship(relsXml, imageRelId, mediaPath);

        if (drawingXml.Contains("name=\"Domain Logo\"", StringComparison.Ordinal))
            return drawingXml;

        const string pictureAnchor =
            """
            <xdr:twoCellAnchor>
              <xdr:from><xdr:col>0</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>0</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:from>
              <xdr:to><xdr:col>3</xdr:col><xdr:colOff>0</xdr:colOff><xdr:row>3</xdr:row><xdr:rowOff>0</xdr:rowOff></xdr:to>
              <xdr:pic macro="">
                <xdr:nvPicPr>
                  <xdr:cNvPr id="1" name="Domain Logo"/>
                  <xdr:cNvPicPr/>
                </xdr:nvPicPr>
                <xdr:blipFill>
                  <a:blip xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:embed="rIdLogo"/>
                  <a:stretch><a:fillRect/></a:stretch>
                </xdr:blipFill>
                <xdr:spPr>
                  <a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/></a:xfrm>
                  <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                </xdr:spPr>
              </xdr:pic>
              <xdr:clientData/>
            </xdr:twoCellAnchor>
            """;

        var insertAt = drawingXml.IndexOf("<xdr:twoCellAnchor", StringComparison.Ordinal);
        if (insertAt < 0)
            return drawingXml;

        return drawingXml.Insert(insertAt, pictureAnchor);
    }

    private static string InjectImageRelationship(string relsXml, string relId, string mediaPath)
    {
        if (relsXml.Contains($"Id=\"{relId}\"", StringComparison.Ordinal))
            return relsXml;

        var target = mediaPath.StartsWith("xl/", StringComparison.Ordinal)
            ? "../" + mediaPath["xl/".Length..]
            : mediaPath;

        const string relType =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

        var rel = $"""
            <Relationship Id="{relId}" Type="{relType}" Target="{target}"/>
            """;

        return relsXml.Replace("</Relationships>", rel + "\n</Relationships>", StringComparison.Ordinal);
    }

    private static string PatchContentTypes(string contentTypesXml, string mediaPath)
    {
        var ext = Path.GetExtension(mediaPath).TrimStart('.').ToLowerInvariant();
        var contentType = ext is "jpg" or "jpeg" ? "image/jpeg" : "image/png";

        if (contentTypesXml.Contains($"Extension=\"{ext}\"", StringComparison.Ordinal))
            return contentTypesXml;

        return contentTypesXml.Replace(
            "<Default Extension=\"rels\"",
            $"<Default Extension=\"{ext}\" ContentType=\"{contentType}\"/><Default Extension=\"rels\"",
            StringComparison.Ordinal);
    }

    private static string NormalizeExtension(string? extension)
    {
        var ext = extension?.Trim() ?? ".png";
        return ext.StartsWith('.') ? ext : "." + ext;
    }

    private static string? ReadTextEntry(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null)
            return null;

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CopyEntry(ZipArchiveEntry entry, ZipArchive writeArchive)
    {
        var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
        using var inStream = entry.Open();
        using var outStream = newEntry.Open();
        inStream.CopyTo(outStream);
    }

    private static void WriteTextEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteBytesEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}
