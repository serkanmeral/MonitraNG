using MongoDB.Bson.Serialization.Attributes;

namespace MngDataGateway.Domain.Entities;

/// <summary>
/// Field-level validation rules
/// </summary>
[BsonIgnoreExtraElements]
public class FieldValidationRules
{
    /// <summary>
    /// Minimum value (for number fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public double? min { get; set; }

    /// <summary>
    /// Maximum value (for number fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public double? max { get; set; }

    /// <summary>
    /// Minimum length (for text fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public int? minLength { get; set; }

    /// <summary>
    /// Maximum length (for text fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public int? maxLength { get; set; }

    /// <summary>
    /// Regex pattern (for text fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public string? pattern { get; set; }

    /// <summary>
    /// Minimum items (for array fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public int? minItems { get; set; }

    /// <summary>
    /// Maximum items (for array fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public int? maxItems { get; set; }

    /// <summary>
    /// Minimum date (for datetime fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public DateTime? minDate { get; set; }

    /// <summary>
    /// Maximum date (for datetime fields)
    /// </summary>
    [BsonIgnoreIfNull]
    public DateTime? maxDate { get; set; }

    /// <summary>
    /// Custom error message (optional)
    /// </summary>
    [BsonIgnoreIfNull]
    public string? message { get; set; }
}

