using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Application.DTOs.DatasetCategory;

/// <summary>
/// Response DTO for dataset category
/// </summary>
public class DatasetCategoryResponseDto
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string DataId { get; set; } = string.Empty;

    /// <summary>
    /// Kategori adı
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Kategori açıklaması
    /// </summary>
    public string? CategoryDescription { get; set; }

    /// <summary>
    /// Sistem kategorisi mi? (Sistem datasetlerinin içinde bulunacağı kategori)
    /// </summary>
    public bool IsSystemCategory { get; set; }

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
}

