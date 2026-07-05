using System.IO.Compression;
using System.Text;

namespace MngDocument.Infrastructure.Services;

/// <summary>Installs or replaces word/footer1.xml and section references on a DOCX package.</summary>
internal static class FooterPartInstaller
{
    internal static byte[] Apply(byte[] docxBytes, string footerXml)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = ReadEntryText(readArchive, "word/document.xml");

            foreach (var entry in readArchive.Entries)
            {
                if (IsReplacedPart(entry.FullName))
                    continue;
                CopyEntry(entry, writeArchive);
            }

            WriteEntry(writeArchive, FooterInjector.FooterPartPathValue, footerXml);
            WriteEntry(writeArchive, "word/_rels/document.xml.rels", BuildDocumentRels(readArchive));
            WriteEntry(writeArchive, "word/document.xml", InjectFooterReference(documentXml));
            WriteEntry(writeArchive, "[Content_Types].xml", BuildContentTypes(readArchive));
        }

        return output.ToArray();
    }

    private static bool IsReplacedPart(string path)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        return normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("word/_rels/document.xml.rels", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(FooterInjector.FooterPartPathValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDocumentRels(ZipArchive archive) =>
        FooterInjector.BuildDocumentRelsPublic(archive);

    private static string InjectFooterReference(string documentXml) =>
        FooterInjector.InjectFooterReferencePublic(documentXml);

    private static string BuildContentTypes(ZipArchive archive) =>
        FooterInjector.BuildContentTypesPublic(archive);

    private static string ReadEntryText(ZipArchive archive, string path)
    {
        var entry = DocxZipHelper.GetRequiredEntry(archive, path);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CopyEntry(ZipArchiveEntry source, ZipArchive target)
    {
        var entry = target.CreateEntry(source.FullName, CompressionLevel.Optimal);
        using var src = source.Open();
        using var dst = entry.Open();
        src.CopyTo(dst);
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
