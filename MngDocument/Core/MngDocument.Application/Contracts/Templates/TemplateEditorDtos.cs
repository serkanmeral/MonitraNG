namespace MngDocument.Application.Contracts.Templates;

public sealed class CreateBlankTemplateRequest
{
    public string CategoryId { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Code { get; init; }
    public TemplateLetterheadDto? Letterhead { get; init; }
    public TemplateFooterDto? Footer { get; init; }
}

public sealed class TemplateEditorSessionDto
{
    public string TemplateId { get; init; } = string.Empty;
    public string EditorUrl { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string WopiSrc { get; init; } = string.Empty;
    public bool ReadOnly { get; init; }
}

public sealed class WopiCheckFileInfoDto
{
    public string BaseFileName { get; init; } = "document.docx";
    public long Size { get; init; }
    public string OwnerId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string UserFriendlyName { get; init; } = string.Empty;
    public string Version { get; init; } = "1";
    public bool SupportsUpdate { get; init; } = true;
    public bool UserCanWrite { get; init; } = true;
    public bool UserCanNotWriteRelative { get; init; }
    public bool SupportsLocks { get; init; }
    public bool DisablePrint { get; init; }
    public bool DisableExport { get; init; }
    public bool UserCanRename { get; init; }
    public bool SupportsRename { get; init; }
    /// <summary>Collabora → host postMessage hedef origin(leri).</summary>
    public string? PostMessageOrigin { get; init; }
}
