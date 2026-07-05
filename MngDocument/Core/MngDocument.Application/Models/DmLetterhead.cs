using System.Text.Json;

namespace MngDocument.Application.Models;

/// <summary><c>dm_letterheads</c> — paylaşımlı antet katalog kaydı.</summary>
public class DmLetterhead
{
    public string? __dataId { get; set; }
    public string? name { get; set; }
    public string? code { get; set; }
    public string? description { get; set; }
    public bool? isDefault { get; set; }
    public bool? isActive { get; set; }
    public string? letterheadJson { get; set; }
    public string? settingsJson { get; set; }
    public JsonElement? designFile { get; set; }
    public string? designStoragePath { get; set; }
    public string? designFileName { get; set; }
    public string? createdBy { get; set; }
    public DateTime? createdAt { get; set; }
    public string? updatedBy { get; set; }
    public DateTime? updatedAt { get; set; }
}
