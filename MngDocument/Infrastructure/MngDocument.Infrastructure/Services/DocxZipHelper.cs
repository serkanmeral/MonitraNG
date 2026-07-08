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
        ZipArchiveEntry? match = null;

        foreach (var entry in archive.Entries)
        {
            var entryPath = NormalizeEntryPath(entry.FullName);
            if (string.Equals(entryPath, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.FullName, normalized.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase))
            {
                // ZipArchive.GetEntry returns the first duplicate; keep the last written part.
                match = entry;
            }
        }

        return match;
    }

    /// <summary>Removes duplicate part names; for duplicates keeps the richest entry (largest payload).</summary>
    internal static byte[] DeduplicateParts(byte[] docxBytes)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);

        var parts = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in readArchive.Entries)
        {
            var key = NormalizeEntryPath(entry.FullName);
            if (!parts.TryGetValue(key, out var existing) || entry.Length > existing.Length)
                parts[key] = entry;
        }

        if (parts.Count == readArchive.Entries.Count)
            return docxBytes;

        using var output = new MemoryStream();
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in parts.Values)
            {
                var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var src = entry.Open();
                using var dst = newEntry.Open();
                src.CopyTo(dst);
            }
        }

        return output.ToArray();
    }

    internal static string NormalizeEntryPath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
