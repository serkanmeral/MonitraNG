namespace MngKeeper.Application.DTOs.Template;

/// <summary>
/// DTO for creating a new template
/// </summary>
public class CreateTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceDomainId { get; set; } = string.Empty;
    public List<SelectedCollectionDto> Collections { get; set; } = new();
}

/// <summary>
/// DTO for selected collection
/// </summary>
public class SelectedCollectionDto
{
    public string CollectionName { get; set; } = string.Empty;
    public bool IncludeIndexes { get; set; } = true;
}
