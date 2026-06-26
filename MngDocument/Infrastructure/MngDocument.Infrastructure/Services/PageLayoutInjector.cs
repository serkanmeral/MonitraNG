using System.IO.Compression;
using System.Text;

namespace MngDocument.Infrastructure.Services;

/// <summary>Applies template page margins and header/footer distances to document.xml sectPr.</summary>
public static class PageLayoutInjector
{
    public static byte[] Apply(byte[] docxBytes, TemplatePageLayoutModel layout)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();
        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
            {
                if (string.Equals(
                        DocxZipHelper.NormalizeEntryPath(entry.FullName),
                        "word/document.xml",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                CopyEntry(entry, writeArchive);
            }

            var documentXml = ReadEntryText(readArchive, "word/document.xml");
            WriteEntry(writeArchive, "word/document.xml", DocxSectPrHelper.ApplyPageLayout(documentXml, layout));
        }

        return output.ToArray();
    }

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
