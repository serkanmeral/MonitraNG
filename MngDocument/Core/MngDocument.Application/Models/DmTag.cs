namespace MngDocument.Application.Models;

/// <summary><c>dm_tags</c> — Document Intelligence etiket kataloğu kaydı.</summary>
public class DmTag
{
    public string? __dataId { get; set; }
    public string? name { get; set; }
    public string? color { get; set; }
    public string? description { get; set; }
    public bool? isActive { get; set; }
    public string? createdBy { get; set; }
    public DateTime? createdAt { get; set; }
    public string? updatedBy { get; set; }
    public DateTime? updatedAt { get; set; }
}
