using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Expands DOCX table template rows using <c>kind=table</c> parameters (G2).
/// Placeholders in the template row: <c>{{paramKey.columnField}}</c>.
/// </summary>
public static class DocxTableExpander
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly Regex TableCellPlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static byte[] Expand(
        byte[] docxBytes,
        TemplateModelDocument model,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> tables)
    {
        if (tables.Count == 0)
            return docxBytes;

        var tableParams = model.Parameters
            .Where(p => IsTableParameter(p) && tables.ContainsKey(p.Key))
            .ToList();

        if (tableParams.Count == 0)
            return docxBytes;

        using var input = new MemoryStream(docxBytes, writable: false);
        using var output = new MemoryStream();

        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in readArchive.Entries)
            {
                var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream = entry.Open();
                using var outStream = newEntry.Open();

                if (string.Equals(entry.FullName, "word/document.xml", StringComparison.OrdinalIgnoreCase))
                {
                    var doc = XDocument.Load(inStream);
                    ExpandInDocument(doc, tableParams, tables);
                    doc.Save(outStream);
                }
                else
                {
                    inStream.CopyTo(outStream);
                }
            }
        }

        return output.ToArray();
    }

    private static bool IsTableParameter(TemplateParameterModel param) =>
        string.Equals(param.Kind, "table", StringComparison.OrdinalIgnoreCase)
        && param.DocBinding is not null
        && string.Equals(param.DocBinding.RegionKind, "table", StringComparison.OrdinalIgnoreCase);

    private static void ExpandInDocument(
        XDocument doc,
        IReadOnlyList<TemplateParameterModel> tableParams,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> tables)
    {
        var body = doc.Descendants(W + "body").FirstOrDefault();
        if (body is null)
            return;

        var docTables = body.Elements(W + "tbl").ToList();

        foreach (var param in tableParams)
        {
            var binding = param.DocBinding!;
            var tableIndex = binding.TableIndex ?? 0;
            if (tableIndex < 0 || tableIndex >= docTables.Count)
                continue;

            var table = docTables[tableIndex];
            var rows = table.Elements(W + "tr").ToList();
            var templateRowIndex = binding.TemplateRowIndex ?? 1;
            if (templateRowIndex < 0 || templateRowIndex >= rows.Count)
                continue;

            var templateRow = rows[templateRowIndex];
            var dataRows = tables.TryGetValue(param.Key, out var data) ? data : Array.Empty<IReadOnlyDictionary<string, object?>>();

            if (dataRows.Count == 0)
            {
                templateRow.Remove();
                continue;
            }

            XElement? insertAfter = templateRow;
            foreach (var dataRow in dataRows)
            {
                var clone = new XElement(templateRow);
                FillRowPlaceholders(clone, param.Key, dataRow, param.ValueSource?.Columns);
                insertAfter!.AddAfterSelf(clone);
                insertAfter = clone;
            }

            templateRow.Remove();
        }
    }

    private static void FillRowPlaceholders(
        XElement row,
        string paramKey,
        IReadOnlyDictionary<string, object?> dataRow,
        IReadOnlyList<TemplateTableColumnModel>? columns)
    {
        foreach (var textNode in row.Descendants(W + "t").ToList())
        {
            textNode.Value = TableCellPlaceholderRegex.Replace(textNode.Value, match =>
            {
                var key = match.Groups[1].Value;
                var field = match.Groups[2].Value;
                if (!string.Equals(key, paramKey, StringComparison.OrdinalIgnoreCase))
                    return match.Value;

                return FormatCellValue(field, dataRow, columns);
            });
        }

        CoalesceParagraphTextNodes(row);
    }

    private static string FormatCellValue(
        string field,
        IReadOnlyDictionary<string, object?> dataRow,
        IReadOnlyList<TemplateTableColumnModel>? columns)
    {
        if (!TryGetFieldValue(dataRow, field, out var raw) || raw is null)
            return string.Empty;

        var format = columns?
            .FirstOrDefault(c => string.Equals(c.SourceField, field, StringComparison.OrdinalIgnoreCase))
            ?.Format;

        if (raw is IFormattable formattable
            && !string.IsNullOrWhiteSpace(format)
            && raw is not string)
        {
            try
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                // fall through
            }
        }

        return raw.ToString()?.Trim() ?? string.Empty;
    }

    private static bool TryGetFieldValue(
        IReadOnlyDictionary<string, object?> dataRow,
        string field,
        out object? value)
    {
        if (dataRow.TryGetValue(field, out value))
            return true;

        foreach (var kv in dataRow)
        {
            if (string.Equals(kv.Key, field, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static void CoalesceParagraphTextNodes(XElement row)
    {
        foreach (var paragraph in row.Descendants(W + "p"))
        {
            var textNodes = paragraph.Descendants(W + "t").ToList();
            if (textNodes.Count <= 1)
                continue;

            var combined = string.Concat(textNodes.Select(t => t.Value));
            textNodes[0].Value = combined;
            for (var i = 1; i < textNodes.Count; i++)
                textNodes[i].Value = string.Empty;
        }
    }
}
