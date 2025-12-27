using MongoDB.Bson.Serialization.Attributes;

namespace MngDataGateway.Domain.Entities;

/// <summary>
/// Query parameter definition
/// </summary>
[BsonIgnoreExtraElements]
public class QueryParameterDefinition
{
    /// <summary>
    /// Parameter name (must match placeholder in pipeline, e.g., ":startDate")
    /// </summary>
    [BsonElement("name")]
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type: "text", "number", "bool", "datetime"
    /// </summary>
    [BsonElement("type")]
    public string type { get; set; } = "text";

    /// <summary>
    /// Parameter description (optional)
    /// </summary>
    [BsonElement("description")]
    [BsonIgnoreIfNull]
    public string? description { get; set; }

    /// <summary>
    /// Is parameter required (default: true)
    /// </summary>
    [BsonElement("required")]
    public bool required { get; set; } = true;
}

