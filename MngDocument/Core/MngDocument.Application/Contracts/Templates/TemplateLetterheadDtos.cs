namespace MngDocument.Application.Contracts.Templates;

public sealed class TemplateLetterheadDto
{
    public bool Enabled { get; init; }
    public bool ShowLogo { get; init; }
    public bool ShowDocumentName { get; init; }
    public bool ShowDocumentNumber { get; init; }
    public bool ShowGeneratedAt { get; init; }
}

public sealed class UpdateTemplateLetterheadRequest
{
    public TemplateLetterheadDto? Letterhead { get; set; }
}
