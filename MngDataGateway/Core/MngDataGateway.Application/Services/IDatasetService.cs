using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Dataset;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Application.Services;

/// <summary>
/// Dataset Service Interface
/// @datasets collection için CRUD operasyonları
/// </summary>
public interface IDatasetService
{
    /// <summary>
    /// Yeni dataset schema oluşturur
    /// </summary>
    Task<DatasetResponseDto> CreateAsync(CreateDatasetDto dto);

    /// <summary>
    /// Dataset schema'larını sayfalı olarak listeler
    /// </summary>
    Task<PagedResultDto<DatasetResponseDto>> GetAllAsync(int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// Name'e göre dataset schema getirir
    /// </summary>
    Task<DatasetResponseDto?> GetByNameAsync(string name);

    /// <summary>
    /// Dataset schema günceller
    /// </summary>
    Task<DatasetResponseDto> UpdateAsync(string name, UpdateDatasetDto dto);

    /// <summary>
    /// Dataset schema'yı siler (hard delete + __deletedDatas backup)
    /// Note: Collection silinmez, sadece schema metadata
    /// </summary>
    Task<bool> DeleteAsync(string name);

    /// <summary>
    /// Silinen dataset schema'yı geri yükler (__deletedDatas'dan)
    /// </summary>
    Task<DatasetResponseDto> RestoreAsync(string name);

    /// <summary>
    /// Name'e göre dataset schema entity getirir (internal use for data operations)
    /// </summary>
    Task<DatasetSchema?> GetSchemaEntityByNameAsync(string name);
}

