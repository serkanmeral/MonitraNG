namespace MngDocument.Application.Models;

/// <summary><c>dm_resource_links</c> satır modeli.</summary>
public class DmResourceLink
{
    public string? __dataId { get; set; }
    public string? resourceId { get; set; }
    public string? targetModule { get; set; }
    public string? targetType { get; set; }
    public string? targetId { get; set; }
    public string? relationType { get; set; }
    public string? createdBy { get; set; }
    public DateTime? createdAt { get; set; }
}
