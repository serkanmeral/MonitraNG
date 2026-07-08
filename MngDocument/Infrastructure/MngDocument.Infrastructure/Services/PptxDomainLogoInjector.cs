using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MngDocument.Infrastructure.Services;

/// <summary>Domain logosunu PPTX kapak slaydına gömer (sağ üst).</summary>
public static class PptxDomainLogoInjector
{
    private const string Slide1Path = "ppt/slides/slide1.xml";
    private const string Slide1RelsPath = "ppt/slides/_rels/slide1.xml.rels";
    private const string ContentTypesPath = "[Content_Types].xml";
    private const string MediaPath = "ppt/media/domain-logo.png";
    private const string LogoRelId = "rIdLogo";

    public static byte[] TryInject(byte[] pptxBytes, byte[] logoBytes, string extension)
    {
        if (logoBytes is not { Length: > 0 })
            return pptxBytes;

        var mediaExt = NormalizeExtension(extension);
        var mediaPath = mediaExt.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || mediaExt.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? "ppt/media/domain-logo.jpg"
            : MediaPath;

        try
        {
            using var input = new MemoryStream(pptxBytes, writable: false);
            using var output = new MemoryStream();

            string? slideXml = null;
            string? slideRelsXml = null;
            string? contentTypesXml = null;

            using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
            {
                slideXml = ReadTextEntry(readArchive, Slide1Path);
                slideRelsXml = ReadTextEntry(readArchive, Slide1RelsPath);
                contentTypesXml = ReadTextEntry(readArchive, ContentTypesPath);

                if (slideXml is null || slideRelsXml is null || contentTypesXml is null)
                    return pptxBytes;

                using var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var entry in readArchive.Entries)
                {
                    if (string.Equals(entry.FullName, Slide1Path, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.FullName, Slide1RelsPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.FullName, ContentTypesPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entry.FullName, mediaPath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    CopyEntry(entry, writeArchive);
                }

                WriteBytesEntry(writeArchive, mediaPath, logoBytes);
                WriteTextEntry(writeArchive, Slide1RelsPath, InjectImageRelationship(slideRelsXml, LogoRelId, mediaPath));
                WriteTextEntry(writeArchive, Slide1Path, InjectBlipIntoLogoShape(slideXml, LogoRelId));
                WriteTextEntry(writeArchive, ContentTypesPath, PatchContentTypes(contentTypesXml, mediaPath));
            }

            return output.ToArray();
        }
        catch
        {
            return pptxBytes;
        }
    }

    private static string InjectBlipIntoLogoShape(string slideXml, string relId)
    {
        if (slideXml.Contains("r:embed=\"" + relId + "\"", StringComparison.Ordinal))
            return slideXml;

        const string blipFragment =
            $"""<a:blip xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:embed="{LogoRelId}"/>""";

        var pattern = new Regex(
            @"(<p:cNvPr[^>]*name=""Domain Logo""[^>]*/>\s*</p:nvPicPr>\s*<p:blipFill>)(\s*<a:stretch)",
            RegexOptions.Singleline);

        if (pattern.IsMatch(slideXml))
            return pattern.Replace(slideXml, m => m.Groups[1].Value + blipFragment + m.Groups[2].Value, 1);

        const string pictureShape =
            $"""
                      <p:pic>
                        <p:nvPicPr>
                          <p:cNvPr id="999" name="Domain Logo"/>
                          <p:cNvPicPr/>
                          <p:nvPr/>
                        </p:nvPicPr>
                        <p:blipFill>
                          {blipFragment}
                          <a:stretch><a:fillRect/></a:stretch>
                        </p:blipFill>
                        <p:spPr>
                          <a:xfrm><a:off x="9000000" y="200000"/><a:ext cx="2800000" cy="1200000"/></a:xfrm>
                          <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                        </p:spPr>
                      </p:pic>
            """;

        var insertAt = slideXml.IndexOf("</p:spTree>", StringComparison.Ordinal);
        return insertAt < 0 ? slideXml : slideXml.Insert(insertAt, pictureShape);
    }

    private static string InjectImageRelationship(string relsXml, string relId, string mediaPath)
    {
        if (relsXml.Contains($"Id=\"{relId}\"", StringComparison.Ordinal))
            return relsXml;

        var target = mediaPath.StartsWith("ppt/", StringComparison.Ordinal)
            ? "../" + mediaPath["ppt/".Length..]
            : mediaPath;

        const string relType =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

        var rel = $"""<Relationship Id="{relId}" Type="{relType}" Target="{target}"/>""";
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
