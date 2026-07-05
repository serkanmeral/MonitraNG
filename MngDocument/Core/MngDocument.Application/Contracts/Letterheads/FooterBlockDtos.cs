namespace MngDocument.Application.Contracts.Letterheads;

public sealed class FooterRunDto
{
    public string Text { get; init; } = string.Empty;
    public bool Bold { get; init; }
}

public sealed class FooterTableCellDto
{
    public IReadOnlyList<FooterRunDto> Runs { get; init; } = Array.Empty<FooterRunDto>();
}

public sealed class FooterTableRowDto
{
    public IReadOnlyList<FooterTableCellDto> Cells { get; init; } = Array.Empty<FooterTableCellDto>();
}

/// <summary>Generic footer block: paragraph | table | divider | spacer.</summary>
public sealed class FooterBlockDto
{
    public string Type { get; init; } = "paragraph";
    public string? Align { get; init; }
    public IReadOnlyList<FooterRunDto>? Runs { get; init; }
    public int? Columns { get; init; }
    public IReadOnlyList<int>? ColumnWidthTwips { get; init; }
    public IReadOnlyList<FooterTableRowDto>? Rows { get; init; }
}
