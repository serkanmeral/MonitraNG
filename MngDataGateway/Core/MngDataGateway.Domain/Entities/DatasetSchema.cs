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

    /// <summary>
    /// Permissions definitions (optional - access control)
    /// null or undefined = no authorization check (everyone can access)
    /// </summary>
    [BsonElement("permissions")]
    [BsonIgnoreIfNull]
    public PermissionsDefinition? permissions { get; set; }

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
    /// Default value (optional) - for JSON deserialization (object type)
    /// </summary>
    [BsonIgnore]
    public object? defaultValue { get; set; }

    /// <summary>
    /// Default value as BsonValue (for MongoDB storage)
    /// </summary>
    [BsonElement("defaultValue")]
    [BsonIgnoreIfNull]
    public MongoDB.Bson.BsonValue? defaultValueBson { get; set; }

    /// <summary>
    /// For relation type: target dataset name
    /// </summary>
    public string? relationDataset { get; set; }

    /// <summary>
    /// For incremental type: options
    /// </summary>
    public IncrementalOptions? incrementalOptions { get; set; }

    /// <summary>
    /// For datetime type: options
    /// </summary>
    [BsonIgnoreIfNull]
    public DateTimeOptions? datetimeOptions { get; set; }

    /// <summary>
    /// Field-level validation rules (optional)
    /// </summary>
    [BsonIgnoreIfNull]
    public FieldValidationRules? validation { get; set; }
}

/// <summary>
/// DateTime field options
/// </summary>
public class DateTimeOptions
{
    /// <summary>
    /// Show time picker (default: true)
    /// If false, only date picker will be shown
    /// </summary>
    public bool showTime { get; set; } = true;
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
    /// Validation description (optional)
    /// </summary>
    [BsonIgnoreIfNull]
    public string? description { get; set; }

    /// <summary>
    /// Validation type: http, expression
    /// </summary>
    public string type { get; set; } = "http";

    /// <summary>
    /// HTTP URL for validation (for type: "http")
    /// </summary>
    [BsonIgnoreIfNull]
    public string? url { get; set; }

    /// <summary>
    /// HTTP method: GET, POST (for type: "http")
    /// </summary>
    [BsonIgnoreIfNull]
    public string? method { get; set; } = "POST";

    /// <summary>
    /// Expression for validation (for type: "expression")
    /// Can reference field names: field1, field2, etc.
    /// Examples: "endDate > startDate", "total == (price * quantity)"
    /// </summary>
    [BsonIgnoreIfNull]
    public string? expression { get; set; }

    /// <summary>
    /// Fields to validate (for type: "http")
    /// </summary>
    [BsonIgnoreIfNull]
    public List<string>? fields { get; set; }

    /// <summary>
    /// When to execute: "create", "update", "both" (default: "both")
    /// </summary>
    [BsonIgnoreIfNull]
    public string? when { get; set; } = "both";

    /// <summary>
    /// Execution order (lower number = earlier execution, default: 0)
    /// </summary>
    [BsonIgnoreIfNull]
    public int? order { get; set; } = 0;
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
    /// Stored as BsonDocument array for MongoDB compatibility
    /// </summary>
    [BsonElement("pipeline")]
    [BsonIgnoreIfNull]
    public List<MongoDB.Bson.BsonDocument>? pipeline { get; set; }

    /// <summary>
    /// Query parameters (with type definitions)
    /// Supports both new format (List<QueryParameterDefinition>) and legacy format (List<string>)
    /// Stored as BsonArray in MongoDB for compatibility
    /// </summary>
    [BsonElement("parameters")]
    [BsonIgnoreIfNull]
    public MongoDB.Bson.BsonArray? parameters { get; set; }
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

/// <summary>
/// Permissions definition for dataset access control
/// </summary>
[BsonIgnoreExtraElements]
public class PermissionsDefinition
{
    /// <summary>
    /// Read permission (GET operations)
    /// </summary>
    [BsonIgnoreIfNull]
    public PermissionDefinition? read { get; set; }

    /// <summary>
    /// Create permission (POST operations)
    /// </summary>
    [BsonIgnoreIfNull]
    public PermissionDefinition? create { get; set; }

    /// <summary>
    /// Update permission (PUT operations)
    /// </summary>
    [BsonIgnoreIfNull]
    public PermissionDefinition? update { get; set; }

    /// <summary>
    /// Delete permission (DELETE operations)
    /// </summary>
    [BsonIgnoreIfNull]
    public PermissionDefinition? delete { get; set; }
}

/// <summary>
/// Permission definition for a specific operation type
/// </summary>
[BsonIgnoreExtraElements]
public class PermissionDefinition
{
    /// <summary>
    /// Allowed group names (from MngKeeper)
    /// Empty array = no one is authorized
    /// </summary>
    [BsonElement("groups")]
    public List<string> groups { get; set; } = new();
}

