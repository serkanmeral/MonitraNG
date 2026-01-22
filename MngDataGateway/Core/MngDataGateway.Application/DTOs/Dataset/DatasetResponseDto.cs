using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// Response DTO for dataset schema
/// </summary>
public class DatasetResponseDto
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string DataId { get; set; } = string.Empty;

    /// <summary>
    /// Dataset name (collection name)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Dataset description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category ID reference
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Force schema validation
    /// </summary>
    public bool ForceSchema { get; set; }

    /// <summary>
    /// Logging mode
    /// </summary>
    public string Logging { get; set; } = string.Empty;

    /// <summary>
    /// Publish mode
    /// </summary>
    public string PublishMode { get; set; } = string.Empty;

    /// <summary>
    /// Field definitions count
    /// </summary>
    public int FieldsCount { get; set; }

    /// <summary>
    /// Field definitions (full details)
    /// </summary>
    public List<FieldDefinition>? Fields { get; set; }

    /// <summary>
    /// Validation rules count
    /// </summary>
    public int ValidationsCount { get; set; }

    /// <summary>
    /// Validation rules (full details)
    /// </summary>
    public List<ValidationDefinition>? Validations { get; set; }

    /// <summary>
    /// Predefined queries count
    /// </summary>
    public int QueriesCount { get; set; }

    /// <summary>
    /// Predefined queries (full details)
    /// </summary>
    public List<QueryDefinitionResponseDto>? Queries { get; set; }

    /// <summary>
    /// Index definitions count
    /// </summary>
    public int IndexListCount { get; set; }

    /// <summary>
    /// Index definitions (full details)
    /// </summary>
    public List<IndexDefinition>? IndexList { get; set; }

    /// <summary>
    /// Creation information
    /// </summary>
    public CreateInfo CreateInfo { get; set; } = null!;

    /// <summary>
    /// Last update information (if updated)
    /// </summary>
    public UpdateInfo? LastUpdateInfo { get; set; }

    /// <summary>
    /// History count
    /// </summary>
    public int HistoryCount { get; set; }

    /// <summary>
    /// Permissions definitions (optional - access control)
    /// null = no authorization check (everyone can access)
    /// </summary>
    public PermissionsDefinition? Permissions { get; set; }
}

