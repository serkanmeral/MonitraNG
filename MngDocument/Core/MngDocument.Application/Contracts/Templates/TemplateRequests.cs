namespace MngDocument.Application.Contracts.Templates;

public sealed class CreateTemplateFromSourceRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SourceResourceId { get; set; }
}

public sealed class CreateTemplateFromReferenceRequest
{
    public string CategoryId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long? Size { get; set; }
}

public sealed class UpdateTemplateParametersRequest
{
    public IReadOnlyList<TemplateParameterDto>? Parameters { get; set; }
}

public sealed class UpdateTemplateMetadataRequest
{
    public string? Name { get; set; }
    public string? Code { get; set; }
}

public sealed class DuplicateTemplateRequest
{
    public string CategoryId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public TemplateLetterheadDto? Letterhead { get; set; }
    public TemplateFooterDto? Footer { get; set; }
    public TemplatePageLayoutDto? PageLayout { get; set; }
}
