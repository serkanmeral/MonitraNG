namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// DTO for QueryDefinition (used in CreateDatasetDto and UpdateDatasetDto)
/// Pipeline is List<object>? to allow JSON deserialization
/// </summary>
public class QueryDefinitionDto
{
    /// <summary>
    /// Query name (unique within dataset)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Query description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// MongoDB aggregation pipeline
    /// List<object>? to allow JSON deserialization (will be converted to List<BsonDocument> in service)
    /// </summary>
    public List<object>? Pipeline { get; set; }

    /// <summary>
    /// Query parameters (with type definitions)
    /// Supports both new format (List<QueryParameterDefinitionDto>) and legacy format (List<string>)
    /// </summary>
    public object? Parameters { get; set; }
}

