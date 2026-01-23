using System.ComponentModel.DataAnnotations;

namespace MngDataGateway.Application.DTOs.DatasetCategory;

/// <summary>
/// DTO for creating a new dataset category
/// </summary>
public class CreateDatasetCategoryDto
{
    /// <summary>
    /// Kategori adı (required, unique)
    /// </summary>
    [Required(ErrorMessage = "Kategori adı zorunludur")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Kategori adı 2-100 karakter arasında olmalıdır")]
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Kategori açıklaması (optional)
    /// </summary>
    [StringLength(500, ErrorMessage = "Açıklama maksimum 500 karakter olabilir")]
    public string? CategoryDescription { get; set; }

    /// <summary>
    /// Sistem kategorisi mi? (Sistem datasetlerinin içinde bulunacağı kategori)
    /// </summary>
    public bool IsSystemCategory { get; set; } = false;
}

