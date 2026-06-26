namespace MngDocument.Application.Models;

/// <summary><c>dm_template_categories</c> — Document Designer katalog ağacı (DI klasörlerinden ayrı).</summary>
public class DmTemplateCategory
{
    public string? __dataId { get; set; }
    public string? parentId { get; set; }
    public List<string>? ancestorIds { get; set; }
    public string? name { get; set; }
    public string? description { get; set; }
    public int? sortOrder { get; set; }
    public string? status { get; set; }
    public string? createdBy { get; set; }
    public DateTime? createdAt { get; set; }
    public string? updatedBy { get; set; }
    public DateTime? updatedAt { get; set; }
}
