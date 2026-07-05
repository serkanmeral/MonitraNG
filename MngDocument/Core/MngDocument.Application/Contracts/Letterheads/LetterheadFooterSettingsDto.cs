namespace MngDocument.Application.Contracts.Letterheads;

/// <summary>Letterhead catalog footer — empty table skeleton; content edited in Collabora.</summary>
public sealed class LetterheadFooterSettingsDto
{
    public bool Enabled { get; init; }
    public int TableRows { get; init; } = 1;
    public int TableColumns { get; init; } = 1;
}
