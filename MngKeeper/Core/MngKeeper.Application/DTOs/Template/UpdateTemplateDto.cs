namespace MngKeeper.Application.DTOs.Template;

/// <summary>
/// DTO for updating a template
/// </summary>
public class UpdateTemplateDto
{
    public string? Description { get; set; }
    public List<SelectedCollectionDto> Collections { get; set; } = new();
}
