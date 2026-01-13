namespace MngKeeper.Application.DTOs;

/// <summary>
/// Template content structure stored in MinIO as JSON
/// </summary>
public class TemplateContent
{
    /// <summary>
    /// Template name
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Collections and their data
    /// </summary>
    public List<CollectionData> Collections { get; set; } = new();
}

/// <summary>
/// Collection data in template
/// </summary>
public class CollectionData
{
    /// <summary>
    /// Collection name
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// All documents in collection (as BSON documents serialized to JSON)
    /// </summary>
    public List<Dictionary<string, object>> Documents { get; set; } = new();

    /// <summary>
    /// Index definitions
    /// </summary>
    public List<IndexDefinition> Indexes { get; set; } = new();
}

/// <summary>
/// Index definition
/// </summary>
public class IndexDefinition
{
    public Dictionary<string, object> Keys { get; set; } = new();
    public bool Unique { get; set; }
    public bool Sparse { get; set; }
    public bool Background { get; set; }
    public string Name { get; set; } = string.Empty;
}
