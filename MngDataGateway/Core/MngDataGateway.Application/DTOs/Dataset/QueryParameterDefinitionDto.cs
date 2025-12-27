namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// DTO for QueryParameterDefinition
/// </summary>
public class QueryParameterDefinitionDto
{
    /// <summary>
    /// Parameter name (must match placeholder in pipeline, e.g., "startDate" for ":startDate")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type: "text", "number", "bool", "datetime"
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// Parameter description (optional)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Is parameter required (default: true)
    /// </summary>
    public bool Required { get; set; } = true;
}

