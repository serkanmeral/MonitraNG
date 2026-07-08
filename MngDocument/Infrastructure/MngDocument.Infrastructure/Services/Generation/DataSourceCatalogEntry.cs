using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DataSourceCatalogEntry
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Provider { get; init; } = "dg";
    public TemplateValueSourceModel Definition { get; init; } = new();
}
