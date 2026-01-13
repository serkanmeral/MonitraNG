using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities;

/// <summary>
/// Template entity for domain initial data
/// Stores template metadata in MongoDB and template content in MinIO
/// </summary>
public class Template
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Template name (unique)
    /// </summary>
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Template description
    /// </summary>
    [BsonElement("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Source domain ID (which domain was used to create this template)
    /// </summary>
    [BsonElement("sourceDomainId")]
    public string SourceDomainId { get; set; } = string.Empty;

    /// <summary>
    /// Source database name (e.g., "mng_meral")
    /// </summary>
    [BsonElement("sourceDatabaseName")]
    public string SourceDatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Selected collections to include in template
    /// </summary>
    [BsonElement("collections")]
    public List<SelectedCollection> Collections { get; set; } = new();

    /// <summary>
    /// MinIO object path where template content is stored
    /// Format: "{SystemFolderPath}/templates/{templateName}.json" (e.g., "System/templates/{templateName}.json")
    /// </summary>
    [BsonElement("minioObjectPath")]
    public string MinIOObjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Total document count in template
    /// </summary>
    [BsonElement("totalDocumentCount")]
    public int TotalDocumentCount { get; set; } = 0;

    /// <summary>
    /// Created timestamp
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Created by user
    /// </summary>
    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Last updated by user
    /// </summary>
    [BsonElement("updatedBy")]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Selected collection configuration for template
/// </summary>
public class SelectedCollection
{
    /// <summary>
    /// Collection name (e.g., "@side_menu", "book")
    /// </summary>
    [BsonElement("collectionName")]
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Include indexes when copying
    /// </summary>
    [BsonElement("includeIndexes")]
    public bool IncludeIndexes { get; set; } = true;

    /// <summary>
    /// Document count in this collection (for preview)
    /// </summary>
    [BsonElement("documentCount")]
    public int DocumentCount { get; set; } = 0;
}
