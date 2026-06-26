using System.IO.Compression;

namespace MngDocument.Infrastructure.Services;

internal static class DocxZipHelper
{
    internal static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string path)
    {
        var entry = GetEntry(archive, path)
                    ?? throw new InvalidOperationException($"Missing DOCX part: {path}");
        return entry;
    }

    internal static ZipArchiveEntry? GetEntry(ZipArchive archive, string path)
    {
        var normalized = NormalizeEntryPath(path);
        return archive.GetEntry(normalized)
               ?? archive.GetEntry(normalized.Replace('/', '\\'));
    }

    internal static string NormalizeEntryPath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
