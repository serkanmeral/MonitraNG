namespace MngDocument.Application.Contracts.CoverPages;

public sealed class CoverPageDesignSessionDto
{
    public string CoverPageId { get; init; } = string.Empty;
    public string EditorUrl { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string WopiSrc { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
}
