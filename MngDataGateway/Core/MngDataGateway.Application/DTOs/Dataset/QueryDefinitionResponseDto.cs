namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// Response DTO for QueryDefinition (pipeline as List<object> for JSON serialization)
/// </summary>
public class QueryDefinitionResponseDto
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
    /// List<object> for JSON serialization (converted from BsonDocument)
    /// </summary>
    public List<object>? Pipeline { get; set; }

    /// <summary>
    /// Query parameters (with type definitions)
    /// Returns List<QueryParameterDefinitionResponseDto> for new format, List<string> for legacy format
    /// </summary>
    public object? Parameters { get; set; }
}

