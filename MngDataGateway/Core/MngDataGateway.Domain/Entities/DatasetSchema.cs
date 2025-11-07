using MngDataGateway.Domain.Entities.Base;
using MongoDB.Bson.Serialization.Attributes;

namespace MngDataGateway.Domain.Entities;

/// <summary>
/// Dataset Schema Entity - @datasets collection
/// Defines the structure and behavior of dynamic data collections
/// </summary>
[BsonIgnoreExtraElements]
public class DatasetSchema : BaseEntity
{
    /// <summary>
    /// Dataset name (unique, e.g., "@tasks", "@users") - REQUIRED
    /// This will be the MongoDB collection name
    /// </summary>
    [BsonElement("name")]
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Dataset description (optional)
    /// </summary>
    [BsonElement("description")]
    public string? description { get; set; }

    /// <summary>
    /// Category ID reference (optional)
    /// References __dataId from @dataset_categories
    /// </summary>
    [BsonElement("category")]
    public string? category { get; set; }

    /// <summary>
    /// Force schema validation (default: true)
    /// true = strict (only defined fields), false = flexible (allow extra fields)
    /// </summary>
    [BsonElement("forceSchema")]
    public bool forceSchema { get; set; } = true;

    /// <summary>
    /// Logging mode: "self", "none", "common" (default: "none")
    /// self = each record has __history, none = no logging, common = @data_logs collection
    /// </summary>
    [BsonElement("logging")]
    public string logging { get; set; } = "none";

    /// <summary>
    /// Publish mode for RabbitMQ events: "none", "basic", "full" (default: "none")
    /// </summary>
    [BsonElement("publish_mode")]
    public string publish_mode { get; set; } = "none";

    /// <summary>
    /// Field definitions (optional - can be empty array)
    /// </summary>
    [BsonElement("fields")]
    public List<FieldDefinition> fields { get; set; } = new();

    /// <summary>
    /// Validation rules (optional - definition only, execution in data controller)
    /// </summary>
    [BsonElement("validations")]
    public List<ValidationDefinition> validations { get; set; } = new();

    /// <summary>
    /// Predefined queries (optional - definition only, execution in data controller)
    /// </summary>
    [BsonElement("queries")]
    public List<QueryDefinition> queries { get; set; } = new();

    /// <summary>
    /// Index definitions (optional - lazy creation on first data insert)
    /// </summary>
    [BsonElement("indexList")]
    public List<IndexDefinition> indexList { get; set; } = new();

    // Helper properties for compatibility (not stored in MongoDB)
    [BsonIgnore]
    public string DatasetName => name;

    [BsonIgnore]
    public string CollectionName => name;

    [BsonIgnore]
    public string? DatasetCategoryCode => category;
}

/// <summary>
/// Field definition in dataset schema
/// </summary>
[BsonIgnoreExtraElements]
public class FieldDefinition
{
    /// <summary>
    /// Field type: text, number, bool, datetime, object, relation, persons, personGroups, incremental
    /// </summary>
    public string fieldType { get; set; } = string.Empty;

    /// <summary>
    /// Field name (unique within dataset)
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Display title
    /// </summary>
    public string? title { get; set; }

    /// <summary>
    /// Is field mandatory
    /// </summary>
    public bool mandatory { get; set; } = false;

    /// <summary>
    /// Is field unique
    /// </summary>
    public bool unique { get; set; } = false;

    /// <summary>
    /// Is field an array
    /// </summary>
    public bool isArray { get; set; } = false;

    /// <summary>
    /// Default value (optional) - stored as BsonValue for MongoDB compatibility
    /// </summary>
    [BsonIgnoreIfNull]
    public MongoDB.Bson.BsonValue? defaultValue { get; set; }

    /// <summary>
    /// For relation type: target dataset name
    /// </summary>
    public string? relationDataset { get; set; }

    /// <summary>
    /// For incremental type: options
    /// </summary>
    public IncrementalOptions? incrementalOptions { get; set; }
}

/// <summary>
/// Incremental field options
/// </summary>
public class IncrementalOptions
{
    /// <summary>
    /// Format template (e.g., "TASK-{0:D6}", "{projectCode}-{year}{month}-{0:D4}")
    /// Placeholders: {0}, {year}, {month}, {day}, {yy}, {domain}, {fieldName}
    /// </summary>
    public string? format { get; set; }

    /// <summary>
    /// Starting value (default: 1)
    /// </summary>
    public int startValue { get; set; } = 1;

    /// <summary>
    /// Increment step (default: 1)
    /// </summary>
    public int incrementStep { get; set; } = 1;
}

/// <summary>
/// Validation definition
/// </summary>
public class ValidationDefinition
{
    /// <summary>
    /// Validation name
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Validation type: http, expression
    /// </summary>
    public string type { get; set; } = "http";

    /// <summary>
    /// HTTP URL for validation
    /// </summary>
    public string? url { get; set; }

    /// <summary>
    /// HTTP method: GET, POST
    /// </summary>
    public string? method { get; set; } = "POST";

    /// <summary>
    /// Fields to validate
    /// </summary>
    public List<string>? fields { get; set; }
}

/// <summary>
/// Query definition
/// </summary>
public class QueryDefinition
{
    /// <summary>
    /// Query name (unique within dataset)
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Query description
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// MongoDB aggregation pipeline
    /// </summary>
    public List<object>? pipeline { get; set; }

    /// <summary>
    /// Query parameters
    /// </summary>
    public List<string>? parameters { get; set; }
}

/// <summary>
/// Index definition
/// </summary>
public class IndexDefinition
{
    /// <summary>
    /// Index name
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Fields to index (field name -> 1 for asc, -1 for desc)
    /// </summary>
    public Dictionary<string, int> fields { get; set; } = new();

    /// <summary>
    /// Is unique index
    /// </summary>
    public bool unique { get; set; } = false;
}

