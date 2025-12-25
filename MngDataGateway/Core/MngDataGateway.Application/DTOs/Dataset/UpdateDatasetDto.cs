using System.ComponentModel.DataAnnotations;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Application.DTOs.Dataset;

/// <summary>
/// DTO for updating an existing dataset schema
/// All fields are optional - only send what needs to be updated
/// </summary>
public class UpdateDatasetDto
{
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
    /// Force schema validation (optional)
    /// </summary>
    public bool? ForceSchema { get; set; }

    /// <summary>
    /// Logging mode (optional)
    /// </summary>
    [RegularExpression(@"^(self|none|common)$", ErrorMessage = "Logging must be: self, none, or common")]
    public string? Logging { get; set; }

    /// <summary>
    /// Publish mode (optional)
    /// </summary>
    [RegularExpression(@"^(none|basic|full)$", ErrorMessage = "Publish mode must be: none, basic, or full")]
    public string? PublishMode { get; set; }

    /// <summary>
    /// Field definitions (optional - replaces entire array if provided)
    /// </summary>
    public List<FieldDefinition>? Fields { get; set; }

    /// <summary>
    /// Validation rules (optional - replaces entire array if provided)
    /// </summary>
    public List<ValidationDefinition>? Validations { get; set; }

    /// <summary>
    /// Predefined queries (optional - replaces entire array if provided)
    /// </summary>
    public List<QueryDefinitionDto>? Queries { get; set; }

    /// <summary>
    /// Index definitions (optional - adds new indexes, keeps old ones)
    /// </summary>
    public List<IndexDefinition>? IndexList { get; set; }
}

