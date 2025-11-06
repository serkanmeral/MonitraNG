using System.ComponentModel.DataAnnotations;

namespace MngDataGateway.Application.DTOs.DatasetCategory;

/// <summary>
/// DTO for updating an existing dataset category
/// </summary>
public class UpdateDatasetCategoryDto
{
    /// <summary>
    /// Kategori adı (optional - sadece değiştirilmek istenirse gönderilir)
    /// </summary>
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Kategori adı 2-100 karakter arasında olmalıdır")]
    public string? CategoryName { get; set; }

    /// <summary>
    /// Kategori açıklaması (optional)
    /// </summary>
    [StringLength(500, ErrorMessage = "Açıklama maksimum 500 karakter olabilir")]
    public string? CategoryDescription { get; set; }
}

