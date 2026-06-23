namespace MngDocument.Application.Contracts.Templates;

public sealed class CreateTemplateFromSourceRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? SourceResourceId { get; set; }
}

public sealed class UpdateTemplateParametersRequest
{
    public IReadOnlyList<TemplateParameterDto>? Parameters { get; set; }
}
