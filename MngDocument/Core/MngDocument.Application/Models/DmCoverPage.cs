using System.Text.Json;

namespace MngDocument.Application.Models;

/// <summary><c>dm_cover_pages</c> — paylaşımlı kapak sayfası katalog kaydı (D-BR2).</summary>
public class DmCoverPage
{
    public string? __dataId { get; set; }
    public string? name { get; set; }
    public string? code { get; set; }
    public string? description { get; set; }
    public bool? isDefault { get; set; }
    public bool? isActive { get; set; }
    public string? coverPageJson { get; set; }
    public string? settingsJson { get; set; }
    public JsonElement? designFile { get; set; }
    public string? designStoragePath { get; set; }
    public string? designFileName { get; set; }
    public string? createdBy { get; set; }
    public DateTime? createdAt { get; set; }
    public string? updatedBy { get; set; }
    public DateTime? updatedAt { get; set; }
}
