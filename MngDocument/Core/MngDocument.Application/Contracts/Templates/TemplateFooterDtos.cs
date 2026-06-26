namespace MngDocument.Application.Contracts.Templates;

public sealed class TemplateFooterDto
{
    public bool Enabled { get; init; }
    public bool ShowFormRevision { get; init; }
    public bool ShowOfficeColumns { get; init; }
    public bool ShowAddresses { get; init; }
    public bool ShowContacts { get; init; }
    public bool ShowDividerLine { get; init; }
}

public sealed class UpdateTemplateFooterRequest
{
    public TemplateFooterDto? Footer { get; set; }
}
