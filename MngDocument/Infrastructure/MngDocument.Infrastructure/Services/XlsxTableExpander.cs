using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Expands XLSX sheet template rows using <c>kind=table</c> + <c>regionKind=sheet</c> parameters (G5).
/// Placeholders in the template row: <c>{{paramKey.columnField}}</c>.
/// </summary>
public static class XlsxTableExpander
{
    private static readonly XNamespace Main =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly Regex TableCellPlaceholderRegex = new(
        @"\{\{([a-zA-Z][a-zA-Z0-9_]*)\.([a-zA-Z][a-zA-Z0-9_]*)\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static byte[] Expand(
        byte[] xlsxBytes,
        TemplateModelDocument model,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> tables)
    {
        if (tables.Count == 0)
            return xlsxBytes;

        var sheetParams = model.Parameters
            .Where(p => IsSheetTableParameter(p) && tables.ContainsKey(p.Key))
            .ToList();

        if (sheetParams.Count == 0)
            return xlsxBytes;

        using var input = new MemoryStream(xlsxBytes, writable: false);
        using var output = new MemoryStream();

        using (var readArchive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var writeArchive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var worksheetPaths = readArchive.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                            && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.Ordinal)
                .Select(e => e.FullName)
                .ToList();

            foreach (var entry in readArchive.Entries)
            {
                var newEntry = writeArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inStream = entry.Open();
                using var outStream = newEntry.Open();

                if (IsWorksheetEntry(entry.FullName, worksheetPaths, out var sheetIndex))
                {
                    var doc = XDocument.Load(inStream);
                    ExpandInWorksheet(doc, sheetIndex, sheetParams, tables);
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

    private static bool IsSheetTableParameter(TemplateParameterModel param) =>
        string.Equals(param.Kind, "table", StringComparison.OrdinalIgnoreCase)
        && param.DocBinding is not null
        && string.Equals(param.DocBinding.RegionKind, "sheet", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorksheetEntry(string fullName, IReadOnlyList<string> worksheetPaths, out int sheetIndex)
    {
        for (var i = 0; i < worksheetPaths.Count; i++)
        {
            if (string.Equals(worksheetPaths[i], fullName, StringComparison.OrdinalIgnoreCase))
            {
                sheetIndex = i;
                return true;
            }
        }

        sheetIndex = -1;
        return false;
    }

    private static void ExpandInWorksheet(
        XDocument doc,
        int sheetIndex,
        IReadOnlyList<TemplateParameterModel> sheetParams,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> tables)
    {
        var sheetData = doc.Descendants(Main + "sheetData").FirstOrDefault();
        if (sheetData is null)
            return;

        var rows = sheetData.Elements(Main + "row").ToList();

        foreach (var param in sheetParams)
        {
            var binding = param.DocBinding!;
            var targetSheetIndex = binding.TableIndex ?? 0;
            if (targetSheetIndex != sheetIndex)
                continue;

            var templateRowIndex = binding.TemplateRowIndex ?? 1;
            if (templateRowIndex < 0 || templateRowIndex >= rows.Count)
                continue;

            var templateRow = rows[templateRowIndex];
            var dataRows = tables.TryGetValue(param.Key, out var data)
                ? data
                : Array.Empty<IReadOnlyDictionary<string, object?>>();

            if (dataRows.Count == 0)
            {
                templateRow.Remove();
                rows = sheetData.Elements(Main + "row").ToList();
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
            rows = sheetData.Elements(Main + "row").ToList();
        }

        RenumberSheetRows(sheetData);
    }

    private static void FillRowPlaceholders(
        XElement row,
        string paramKey,
        IReadOnlyDictionary<string, object?> dataRow,
        IReadOnlyList<TemplateTableColumnModel>? columns)
    {
        foreach (var textNode in row.Descendants(Main + "t").ToList())
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

    private static void RenumberSheetRows(XElement sheetData)
    {
        var rows = sheetData.Elements(Main + "row").ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            var rowNum = i + 1;
            rows[i].SetAttributeValue("r", rowNum);
            foreach (var cell in rows[i].Elements(Main + "c"))
            {
                var col = GetColumnLetters(cell.Attribute("r")?.Value);
                if (!string.IsNullOrEmpty(col))
                    cell.SetAttributeValue("r", col + rowNum);
            }
        }
    }

    private static string GetColumnLetters(string? cellRef)
    {
        if (string.IsNullOrWhiteSpace(cellRef))
            return "A";

        var i = 0;
        while (i < cellRef.Length && char.IsLetter(cellRef[i]))
            i++;

        return i > 0 ? cellRef[..i] : "A";
    }
}
