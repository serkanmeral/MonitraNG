using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MngDocument.Infrastructure.Services;

/// <summary>Prepends a cover DOCX as the first section before the body document.</summary>
public static class CoverPageMerger
{
    private static readonly XNamespace W = DocxSectPrHelper.W;
    private static readonly XNamespace R =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace RelNs =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    private const string DocumentRelsPath = "word/_rels/document.xml.rels";
    private const string ContentTypesPath = "[Content_Types].xml";

    public static byte[] Prepend(byte[] bodyDocx, byte[] coverDocx)
    {
        if (coverDocx is not { Length: > 0 })
            return bodyDocx;

        try
        {
            var coverDocumentXml = ReadDocumentXml(coverDocx);
            var bodyDocumentXml = ReadDocumentXml(bodyDocx);
            var relIdMap = new Dictionary<string, string>(StringComparer.Ordinal);

            using var bodyInput = new MemoryStream(bodyDocx, writable: false);
            using var coverInput = new MemoryStream(coverDocx, writable: false);
            using var output = new MemoryStream();

            using (var bodyArchive = new ZipArchive(bodyInput, ZipArchiveMode.Read, leaveOpen: true))
            using (var coverArchive = new ZipArchive(coverInput, ZipArchiveMode.Read, leaveOpen: true))
            using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                var mergedRels = MergeDocumentRels(
                    ReadEntryText(bodyArchive, DocumentRelsPath),
                    ReadEntryText(coverArchive, DocumentRelsPath),
                    relIdMap);

                var mergedDocumentXml = MergeDocumentXml(coverDocumentXml, bodyDocumentXml);
                if (relIdMap.Count > 0)
                    mergedDocumentXml = RemapEmbedIds(mergedDocumentXml, relIdMap);
                var copiedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in bodyArchive.Entries)
                {
                    if (IsMergedPart(entry.FullName))
                        continue;

                    CopyEntry(entry, writeArchive);
                    copiedPaths.Add(entry.FullName);
                }

                foreach (var entry in coverArchive.Entries)
                {
                    if (IsMergedPart(entry.FullName))
                        continue;

                    if (copiedPaths.Contains(entry.FullName))
                        continue;

                    if (entry.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase)
                        || entry.FullName.StartsWith("word/theme/", StringComparison.OrdinalIgnoreCase)
                        || entry.FullName.StartsWith("word/fontTable", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyEntry(entry, writeArchive);
                        copiedPaths.Add(entry.FullName);
                    }
                }

                var mergedContentTypes = MergeContentTypes(
                    ReadEntryText(bodyArchive, ContentTypesPath),
                    ReadEntryText(coverArchive, ContentTypesPath));

                WriteTextEntry(writeArchive, "word/document.xml", mergedDocumentXml);
                WriteTextEntry(writeArchive, DocumentRelsPath, mergedRels);
                WriteTextEntry(writeArchive, ContentTypesPath, mergedContentTypes);
            }

            return output.ToArray();
        }
        catch
        {
            return bodyDocx;
        }
    }

    private static bool IsMergedPart(string path)
    {
        var normalized = DocxZipHelper.NormalizeEntryPath(path);
        return normalized.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(DocumentRelsPath, StringComparison.OrdinalIgnoreCase)
               || normalized.Equals(ContentTypesPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string MergeDocumentXml(string coverDocumentXml, string bodyDocumentXml)
    {
        var coverDoc = XDocument.Parse(coverDocumentXml);
        var bodyDoc = XDocument.Parse(bodyDocumentXml);

        var coverBody = coverDoc.Root?.Element(W + "body")
                        ?? throw new InvalidOperationException("Invalid cover document.xml");
        var bodyBody = bodyDoc.Root?.Element(W + "body")
                       ?? throw new InvalidOperationException("Invalid body document.xml");

        var coverElements = coverBody.Elements()
            .Where(e => e.Name != W + "sectPr")
            .Select(e => new XElement(e))
            .ToList();

        var bodySectPr = bodyBody.Elements(W + "sectPr").LastOrDefault();
        var bodyElements = bodyBody.Elements()
            .Where(e => e.Name != W + "sectPr")
            .Select(e => new XElement(e))
            .ToList();

        var coverSectionBreak = new XElement(W + "p",
            new XElement(W + "pPr",
                new XElement(W + "sectPr",
                    new XElement(W + "type", new XAttribute(W + "val", "nextPage")),
                    new XElement(W + "titlePg"))));

        var mergedBody = new XElement(W + "body");
        foreach (var element in coverElements)
            mergedBody.Add(element);
        mergedBody.Add(coverSectionBreak);
        foreach (var element in bodyElements)
            mergedBody.Add(element);

        if (bodySectPr is not null)
            mergedBody.Add(new XElement(bodySectPr));

        var mergedRoot = new XElement(bodyDoc.Root!.Name, bodyDoc.Root.Attributes(), mergedBody);
        return mergedRoot.ToString(SaveOptions.DisableFormatting);
    }

    private static string RemapEmbedIds(string documentXml, IReadOnlyDictionary<string, string> relIdMap)
    {
        if (relIdMap.Count == 0)
            return documentXml;

        var doc = XDocument.Parse(documentXml);
        foreach (var blip in doc.Descendants().Where(e => e.Name.LocalName == "blip"))
        {
            var embedAttr = blip.Attributes().FirstOrDefault(a =>
                a.Name.LocalName == "embed"
                && (a.Name.Namespace == R || a.Name.NamespaceName.Contains("relationships", StringComparison.OrdinalIgnoreCase)));
            if (embedAttr is null)
                continue;

            var oldId = embedAttr.Value;
            if (relIdMap.TryGetValue(oldId, out var newId))
                embedAttr.SetValue(newId);
        }

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string MergeDocumentRels(
        string bodyRelsXml,
        string coverRelsXml,
        Dictionary<string, string> relIdMap)
    {
        var bodyDoc = XDocument.Parse(bodyRelsXml);
        var coverDoc = XDocument.Parse(coverRelsXml);
        var bodyRoot = bodyDoc.Root ?? throw new InvalidOperationException("Invalid body document rels.");

        var usedIds = bodyRoot.Elements(RelNs + "Relationship")
            .Select(r => (string?)r.Attribute("Id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);

        var usedTargets = bodyRoot.Elements(RelNs + "Relationship")
            .Select(r => NormalizeRelTarget((string?)r.Attribute("Target")))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        foreach (var rel in coverDoc.Root?.Elements(RelNs + "Relationship") ?? [])
        {
            var type = (string?)rel.Attribute("Type") ?? string.Empty;
            if (!type.Contains("/image", StringComparison.OrdinalIgnoreCase))
                continue;

            var target = NormalizeRelTarget((string?)rel.Attribute("Target"));
            if (string.IsNullOrWhiteSpace(target) || usedTargets.Contains(target))
                continue;

            var sourceId = (string?)rel.Attribute("Id") ?? string.Empty;
            var resolvedId = usedIds.Contains(sourceId) ? AllocateRelId(usedIds) : sourceId;
            if (!string.Equals(sourceId, resolvedId, StringComparison.Ordinal))
                relIdMap[sourceId] = resolvedId;

            bodyRoot.Add(new XElement(RelNs + "Relationship",
                new XAttribute("Id", resolvedId),
                new XAttribute("Type", type),
                new XAttribute("Target", target)));

            usedIds.Add(resolvedId);
            usedTargets.Add(target);
        }

        return bodyDoc.ToString(SaveOptions.DisableFormatting);
    }

    private static string MergeContentTypes(string bodyContentTypesXml, string coverContentTypesXml)
    {
        var bodyDoc = XDocument.Parse(bodyContentTypesXml);
        var coverDoc = XDocument.Parse(coverContentTypesXml);
        var bodyRoot = bodyDoc.Root ?? throw new InvalidOperationException("Invalid body content types.");

        var existingPartNames = bodyRoot.Elements()
            .Select(e => (string?)e.Attribute("PartName"))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        var existingExtensions = bodyRoot.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase))
            .Select(e => (string?)e.Attribute("Extension"))
            .Where(ext => !string.IsNullOrWhiteSpace(ext))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

        foreach (var node in coverDoc.Root?.Elements() ?? [])
        {
            if (string.Equals(node.Name.LocalName, "Override", StringComparison.OrdinalIgnoreCase))
            {
                var partName = (string?)node.Attribute("PartName");
                if (string.IsNullOrWhiteSpace(partName) || existingPartNames.Contains(partName))
                    continue;

                bodyRoot.Add(new XElement(node));
                existingPartNames.Add(partName);
                continue;
            }

            if (string.Equals(node.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase))
            {
                var extension = (string?)node.Attribute("Extension");
                if (string.IsNullOrWhiteSpace(extension) || existingExtensions.Contains(extension))
                    continue;

                bodyRoot.Add(new XElement(node));
                existingExtensions.Add(extension);
            }
        }

        return bodyDoc.ToString(SaveOptions.DisableFormatting);
    }

    private static string NormalizeRelTarget(string? target) =>
        (target ?? string.Empty).Replace('\\', '/').Trim();

    private static string AllocateRelId(HashSet<string> usedIds)
    {
        for (var i = 1; i < 500; i++)
        {
            var candidate = $"rId{i}";
            if (!usedIds.Contains(candidate))
                return candidate;
        }

        return $"rId{Guid.NewGuid():N}"[..8];
    }

    private static string ReadDocumentXml(byte[] docxBytes) =>
        ReadEntryTextFromBytes(docxBytes, "word/document.xml");

    private static string ReadEntryText(ZipArchive archive, string path)
    {
        var entry = DocxZipHelper.GetRequiredEntry(archive, path);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ReadEntryTextFromBytes(byte[] docxBytes, string path)
    {
        using var input = new MemoryStream(docxBytes, writable: false);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        return ReadEntryText(archive, path);
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
}
