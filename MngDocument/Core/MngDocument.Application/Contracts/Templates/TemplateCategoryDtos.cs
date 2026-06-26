namespace MngDocument.Application.Contracts.Templates;

public sealed class TemplateCategoryDto
{
    public string Id { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public IReadOnlyList<string> AncestorIds { get; init; } = Array.Empty<string>();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public string Status { get; init; } = "active";
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class TemplateCategoryTreeNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public List<TemplateCategoryTreeNodeDto> Children { get; init; } = new();
}
