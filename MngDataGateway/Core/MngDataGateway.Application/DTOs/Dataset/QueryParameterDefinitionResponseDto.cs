namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// Response DTO for QueryParameterDefinition
/// </summary>
public class QueryParameterDefinitionResponseDto
{
    /// <summary>
    /// Parameter name
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
    /// Is parameter required
    /// </summary>
    public bool Required { get; set; } = true;
}

