using MngDocument.Application.Contracts.Letterheads;

namespace MngDocument.Infrastructure.Services;

/// <summary>Renders generic footer blocks (paragraph/table/divider/spacer) into DOCX footer1.xml.</summary>
public static class GenericFooterBlockRenderer
{
    private const int ContentWidthTwips = 8316;
    private const int DefaultColumnWidthTwips = ContentWidthTwips / 2;

    private const string RevisionRunProps =
        "<w:rPr><w:sz w:val=\"14\"/><w:szCs w:val=\"12\"/></w:rPr>";

    private const string SpacerRunProps =
        "<w:rPr><w:sz w:val=\"16\"/><w:szCs w:val=\"16\"/></w:rPr>";

    private const string BoldRunProps =
        "<w:rPr><w:rFonts w:ascii=\"Tahoma\" w:hAnsi=\"Tahoma\" w:cs=\"Tahoma\"/><w:b/><w:color w:val=\"231F20\"/><w:w w:val=\"80\"/><w:sz w:val=\"16\"/><w:szCs w:val=\"16\"/></w:rPr>";

    private const string NormalRunProps =
        "<w:rPr><w:rFonts w:ascii=\"Tahoma\" w:hAnsi=\"Tahoma\" w:cs=\"Tahoma\"/><w:color w:val=\"231F20\"/><w:w w:val=\"80\"/><w:kern w:val=\"22\"/><w:sz w:val=\"16\"/><w:szCs w:val=\"16\"/></w:rPr>";

    public static byte[] Apply(
        byte[] docxBytes,
        IReadOnlyList<FooterBlockDto> blocks,
        TemplatePageLayoutModel layout)
    {
        if (blocks.Count == 0)
            return docxBytes;

        var tableRows = BuildTableRows(blocks);
        if (tableRows.Count == 0)
            return docxBytes;

        tableRows.Add(BuildMergedRow(string.Empty, SpacerRunProps));

        var footerXml = $"""
                          <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                          <w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                            {BuildFooterTable(tableRows, layout.FooterLeftIndentTwips)}
                          </w:ftr>
                          """;

        return FooterPartInstaller.Apply(docxBytes, footerXml);
    }

    public static IReadOnlyList<string> ExtractPreviewLines(IReadOnlyList<FooterBlockDto> blocks)
    {
        var lines = new List<string>();
        foreach (var block in blocks)
        {
            var type = block.Type?.Trim().ToLowerInvariant() ?? "paragraph";
            switch (type)
            {
                case "paragraph":
                    AppendRunLines(lines, block.Runs);
                    break;
                case "table":
                    if (block.Rows is null)
                        break;
                    foreach (var row in block.Rows)
                    {
                        var cellTexts = row.Cells
                            .Select(c => string.Join(' ', c.Runs.Select(r => r.Text.Trim())).Trim())
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();
                        if (cellTexts.Count > 0)
                            lines.Add(string.Join("  ·  ", cellTexts));
                    }
                    break;
                case "divider":
                    lines.Add("—");
                    break;
            }
        }

        return lines;
    }

    private static List<string> BuildTableRows(IReadOnlyList<FooterBlockDto> blocks)
    {
        var rows = new List<string>();
        foreach (var block in blocks)
        {
            var type = block.Type?.Trim().ToLowerInvariant() ?? "paragraph";
            switch (type)
            {
                case "paragraph":
                    rows.Add(BuildParagraphRow(block));
                    break;
                case "table":
                    rows.AddRange(BuildTableBlockRows(block));
                    break;
                case "divider":
                    rows.Add(BuildDividerRow());
                    break;
                case "spacer":
                    rows.Add(BuildMergedRow(string.Empty, SpacerRunProps));
                    break;
            }
        }

        return rows;
    }

    private static string BuildParagraphRow(FooterBlockDto block)
    {
        var align = NormalizeAlign(block.Align);
        var text = string.Concat(block.Runs?.Select(r => EscapeXml(r.Text)) ?? Array.Empty<string>());
        var runProps = RevisionRunProps;
        if (block.Runs?.Any(r => r.Bold) == true)
            runProps = BoldRunProps;

        return $"""
                  <w:tr>
                    <w:tc>
                      <w:tcPr>
                        <w:gridSpan w:val="2"/>
                        <w:tcW w:w="{ContentWidthTwips}" w:type="dxa"/>
                      </w:tcPr>
                      <w:p>
                        <w:pPr><w:jc w:val="{align}"/></w:pPr>
                        <w:r>{runProps}<w:t xml:space="preserve">{text}</w:t></w:r>
                      </w:p>
                    </w:tc>
                  </w:tr>
                  """;
    }

    private static IEnumerable<string> BuildTableBlockRows(FooterBlockDto block)
    {
        var columns = block.Columns is > 0 ? block.Columns!.Value : 2;
        var columnWidths = ResolveColumnWidths(block, columns);
        if (block.Rows is null)
            yield break;

        foreach (var row in block.Rows)
        {
            var cells = row.Cells.Take(columns).ToList();
            while (cells.Count < columns)
                cells.Add(new FooterTableCellDto());

            var cellXml = new List<string>();
            for (var i = 0; i < columns; i++)
            {
                var runs = cells[i].Runs;
                var text = string.Concat(runs.Select(r => EscapeXml(r.Text)));
                var bold = runs.Any(r => r.Bold);
                cellXml.Add(BuildTableCell(text, bold ? BoldRunProps : NormalRunProps, columnWidths[i]));
            }

            yield return $"""
                          <w:tr>
                            {string.Concat(cellXml)}
                          </w:tr>
                          """;
        }
    }

    private static int[] ResolveColumnWidths(FooterBlockDto block, int columns)
    {
        if (block.ColumnWidthTwips is { Count: > 0 } widths)
        {
            var result = new int[columns];
            for (var i = 0; i < columns; i++)
                result[i] = i < widths.Count && widths[i] > 0 ? widths[i] : DefaultColumnWidthTwips;
            return result;
        }

        var even = ContentWidthTwips / columns;
        return Enumerable.Repeat(even, columns).ToArray();
    }

    private static string BuildFooterTable(IEnumerable<string> rows, int leftIndentTwips)
    {
        var colWidth = DefaultColumnWidthTwips;
        return $"""
                  <w:tbl>
                    <w:tblPr>
                      <w:tblW w:w="5000" w:type="pct"/>
                      <w:tblInd w:w="{leftIndentTwips}" w:type="dxa"/>
                      <w:tblLayout w:type="fixed"/>
                      <w:tblCellMar>
                        <w:top w:w="0" w:type="dxa"/>
                        <w:left w:w="0" w:type="dxa"/>
                        <w:bottom w:w="0" w:type="dxa"/>
                        <w:right w:w="0" w:type="dxa"/>
                      </w:tblCellMar>
                      <w:tblLook w:val="04A0" w:firstRow="1" w:lastRow="0" w:firstColumn="1" w:lastColumn="0" w:noHBand="0" w:noVBand="1"/>
                    </w:tblPr>
                    <w:tblGrid>
                      <w:gridCol w:w="{colWidth}"/>
                      <w:gridCol w:w="{colWidth}"/>
                    </w:tblGrid>
                    {string.Concat(rows)}
                  </w:tbl>
                  """;
    }

    private static string BuildMergedRow(string text, string runProps) =>
        $"""
         <w:tr>
           <w:tc>
             <w:tcPr>
               <w:gridSpan w:val="2"/>
               <w:tcW w:w="{ContentWidthTwips}" w:type="dxa"/>
             </w:tcPr>
             <w:p>
               <w:pPr><w:jc w:val="both"/></w:pPr>
               <w:r>{runProps}<w:t xml:space="preserve">{text}</w:t></w:r>
             </w:p>
           </w:tc>
         </w:tr>
         """;

    private static string BuildDividerRow() =>
        $"""
         <w:tr>
           <w:tc>
             <w:tcPr>
               <w:gridSpan w:val="2"/>
               <w:tcW w:w="{ContentWidthTwips}" w:type="dxa"/>
             </w:tcPr>
             <w:p>
               <w:pPr>
                 <w:pBdr>
                   <w:top w:val="single" w:sz="12" w:space="1" w:color="231F20"/>
                 </w:pBdr>
               </w:pPr>
             </w:p>
           </w:tc>
         </w:tr>
         """;

    private static string BuildTableCell(string text, string runProps, int widthTwips) =>
        $"""
         <w:tc>
           <w:tcPr>
             <w:tcW w:w="{widthTwips}" w:type="dxa"/>
             <w:vAlign w:val="top"/>
           </w:tcPr>
           <w:p>
             <w:pPr><w:jc w:val="both"/></w:pPr>
             <w:r>{runProps}<w:t xml:space="preserve">{text}</w:t></w:r>
           </w:p>
         </w:tc>
         """;

    private static void AppendRunLines(List<string> lines, IReadOnlyList<FooterRunDto>? runs)
    {
        if (runs is null || runs.Count == 0)
            return;

        var text = string.Join(' ', runs.Select(r => r.Text.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)));
        if (!string.IsNullOrWhiteSpace(text))
            lines.Add(text);
    }

    private static string NormalizeAlign(string? align) =>
        align?.Trim().ToLowerInvariant() switch
        {
            "center" => "center",
            "right" => "right",
            "left" => "left",
            _ => "both"
        };

    private static string EscapeXml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
