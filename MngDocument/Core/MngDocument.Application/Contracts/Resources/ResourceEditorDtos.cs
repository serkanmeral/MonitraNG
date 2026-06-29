namespace MngDocument.Application.Contracts.Resources;

public sealed class ResourceEditorSessionDto
{
    public string ResourceId { get; init; } = string.Empty;
    public string EditorUrl { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string WopiSrc { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
}
