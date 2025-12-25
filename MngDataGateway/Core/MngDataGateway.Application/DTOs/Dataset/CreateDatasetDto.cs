using System.ComponentModel.DataAnnotations;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// DTO for creating a new dataset schema
/// </summary>
public class CreateDatasetDto
{
    /// <summary>
    /// Dataset name (unique, e.g., "@tasks") - REQUIRED
    /// </summary>
    [Required(ErrorMessage = "Dataset name zorunludur")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Dataset name 2-100 karakter arasında olmalıdır")]
    [RegularExpression(@"^@?[a-zA-Z][a-zA-Z0-9_-]*$", ErrorMessage = "Dataset name geçersiz format (ör: @tasks, @users)")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Dataset description (optional)
    /// </summary>
    [StringLength(1000, ErrorMessage = "Description maksimum 1000 karakter olabilir")]
    public string? Description { get; set; }

    /// <summary>
    /// Category ID reference (optional)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Force schema validation (default: true)
    /// </summary>
    public bool ForceSchema { get; set; } = true;

    /// <summary>
    /// Logging mode (default: "none")
    /// </summary>
    [RegularExpression(@"^(self|none|common)$", ErrorMessage = "Logging must be: self, none, or common")]
    public string Logging { get; set; } = "none";

    /// <summary>
    /// Publish mode (default: "none")
    /// </summary>
    [RegularExpression(@"^(none|basic|full)$", ErrorMessage = "Publish mode must be: none, basic, or full")]
    public string PublishMode { get; set; } = "none";

    /// <summary>
    /// Field definitions (optional)
    /// </summary>
    public List<FieldDefinition>? Fields { get; set; }

    /// <summary>
    /// Validation rules (optional)
    /// </summary>
    public List<ValidationDefinition>? Validations { get; set; }

    /// <summary>
    /// Predefined queries (optional)
    /// </summary>
    public List<QueryDefinitionDto>? Queries { get; set; }

    /// <summary>
    /// Index definitions (optional)
    /// </summary>
    public List<IndexDefinition>? IndexList { get; set; }
}

