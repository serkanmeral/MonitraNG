namespace MngDocument.Application.Contracts.Letterheads;

public sealed class LetterheadDesignSessionDto
{
    public string LetterheadId { get; init; } = string.Empty;
    public string EditorUrl { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string WopiSrc { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
    /// <summary>custom | programmatic | disabled</summary>
    public string DesignFooterSource { get; init; } = "programmatic";
    public IReadOnlyList<string> FooterPreviewLines { get; init; } = Array.Empty<string>();
}
