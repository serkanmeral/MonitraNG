using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.DatasetCategory;

namespace MngDataGateway.Application.Services;

/// <summary>
/// Dataset Category Service Interface
/// @dataset_categories collection için CRUD operasyonları
/// </summary>
public interface IDatasetCategoryService
{
    /// <summary>
    /// Yeni kategori oluşturur
    /// </summary>
    Task<DatasetCategoryResponseDto> CreateAsync(CreateDatasetCategoryDto dto);

    /// <summary>
    /// Kategorileri sayfalı olarak listeler
    /// </summary>
    Task<PagedResultDto<DatasetCategoryResponseDto>> GetAllAsync(int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// ID'ye göre kategori getirir
    /// </summary>
    Task<DatasetCategoryResponseDto?> GetByIdAsync(string dataId);

    /// <summary>
    /// Kategori günceller
    /// </summary>
    Task<DatasetCategoryResponseDto> UpdateAsync(string dataId, UpdateDatasetCategoryDto dto);

    /// <summary>
    /// Kategoriyi siler (hard delete + __deletedDatas backup)
    /// </summary>
    Task<bool> DeleteAsync(string dataId);

    /// <summary>
    /// Silinen kategoriyi geri yükler (__deletedDatas'dan)
    /// </summary>
    Task<DatasetCategoryResponseDto> RestoreAsync(string dataId);
}

