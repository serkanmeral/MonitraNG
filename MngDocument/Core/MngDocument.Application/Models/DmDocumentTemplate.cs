namespace MngDocument.Application.Models;

/// <summary><c>dm_document_templates</c> DG kaydı.</summary>
public class DmDocumentTemplate
{
    public string? __dataId { get; set; }
    public string? name { get; set; }
    public string? description { get; set; }
    public string? sourceResourceId { get; set; }
    public string? sourceFileName { get; set; }
    public string? creationMode { get; set; }
    public string? status { get; set; }
    public string? modelJson { get; set; }
    public string? createdBy { get; set; }
    public DateTime? createdAt { get; set; }
    public string? updatedBy { get; set; }
    public DateTime? updatedAt { get; set; }
}
