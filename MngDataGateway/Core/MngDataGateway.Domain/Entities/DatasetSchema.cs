namespace MngDataGateway.Domain.Entities;

/// <summary>
/// Dataset Schema Entity - @datasets collection
/// </summary>
public class DatasetSchema
{
    /// <summary>
    /// Unique identifier for dataset (GUID)
    /// </summary>
    public string __dataId { get; set; } = string.Empty;

    /// <summary>
    /// Category ID (optional)
    /// </summary>
    public string? category { get; set; }

    /// <summary>
    /// Dataset name (unique, e.g., "@tasks", "@users")
    /// </summary>
    public string name { get; set; } = string.Empty;

    /// <summary>
    /// Dataset description
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// Force schema validation
    /// </summary>
    public bool forceSchema { get; set; } = false;

    /// <summary>
    /// Logging mode: "self", "none", "common"
    /// </summary>
    public string logging { get; set; } = "none";

    /// <summary>
    /// Publish mode for events: "none", "basic", "full"
    /// </summary>
    public string publish_mode { get; set; } = "none";

    /// <summary>
    /// Field definitions
    /// </summary>
    public List<FieldDefinition> fields { get; set; } = new();

    /// <summary>
    /// Validation rules
    /// </summary>
    public List<ValidationDefinition> validations { get; set; } = new();

    /// <summary>
    /// Predefined queries
    /// </summary>
    public List<QueryDefinition> queries { get; set; } = new();

    /// <summary>
    /// Index definitions
    /// </summary>
    public List<IndexDefinition> indexList { get; set; } = new();

    /// <summary>
    /// Creation metadata
    /// </summary>
    public DateTime? createdAt { get; set; }

    /// <summary>
    /// Update metadata
    /// </summary>
    public DateTime? updatedAt { get; set; }

    /// <summary>
    /// Created by user ID
    /// </summary>
    public string? createdBy { get; set; }

    /// <summary>
    /// Updated by user ID
    /// </summary>
    public string? updatedBy { get; set; }
}

/// <summary>
/// Field definition in dataset schema
/// </summary>
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
    /// Default value (optional)
    /// </summary>
    public object? defaultValue { get; set; }

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
    /// Format template (e.g., "TASK-{0:D6}", "INV-{year}{month}-{0:D4}")
    /// </summary>
    public string? format { get; set; }

    /// <summary>
    /// Starting value
    /// </summary>
    public int startValue { get; set; } = 1;

    /// <summary>
    /// Reset period: none, daily, monthly, yearly
    /// </summary>
    public string resetPeriod { get; set; } = "none";
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

