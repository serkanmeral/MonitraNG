namespace MngKeeper.Application.DTOs.Template;

/// <summary>
/// DTO for template response
/// </summary>
public class TemplateResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceDomainId { get; set; } = string.Empty;
    public string SourceDatabaseName { get; set; } = string.Empty;
    public List<SelectedCollectionResponseDto> Collections { get; set; } = new();
    public int TotalDocumentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// DTO for selected collection response
/// </summary>
public class SelectedCollectionResponseDto
{
    public string CollectionName { get; set; } = string.Empty;
    public bool IncludeIndexes { get; set; }
    public int DocumentCount { get; set; }
}
