using MngDocument.Application.Contracts.Letterheads;

namespace MngDocument.Infrastructure.Services;

/// <summary>Builds empty footer table skeleton into design DOCX footer1.xml.</summary>
public static class LetterheadFooterTableBuilder
{
    public static byte[] ApplyEmptyTable(
        byte[] docxBytes,
        int rows,
        int columns,
        TemplatePageLayoutModel layout)
    {
        rows = Math.Clamp(rows, 1, 12);
        columns = Math.Clamp(columns, 1, 6);

        var tableRows = new List<FooterTableRowDto>();
        for (var r = 0; r < rows; r++)
        {
            var cells = new List<FooterTableCellDto>();
            for (var c = 0; c < columns; c++)
                cells.Add(new FooterTableCellDto { Runs = [new FooterRunDto { Text = " " }] });
            tableRows.Add(new FooterTableRowDto { Cells = cells });
        }

        var block = new FooterBlockDto
        {
            Type = "table",
            Columns = columns,
            Rows = tableRows
        };

        return GenericFooterBlockRenderer.Apply(docxBytes, [block], layout);
    }
}
