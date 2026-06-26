namespace MngDocument.Application.Contracts.Templates;

public sealed class CreateTemplateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ParentId { get; set; }
}

public sealed class RenameTemplateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class MoveTemplateCategoryRequest
{
    public string? NewParentId { get; set; }
}
