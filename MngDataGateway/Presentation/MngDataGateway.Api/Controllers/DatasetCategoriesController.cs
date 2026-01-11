using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Api.Helpers;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.DatasetCategory;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/dataset-categories")]
[Authorize]
[Produces("application/json")]
public class DatasetCategoriesController : ControllerBase
{
    private readonly IDatasetCategoryService _categoryService;
    private readonly ILogger<DatasetCategoriesController> _logger;

    public DatasetCategoriesController(
        IDatasetCategoryService categoryService,
        ILogger<DatasetCategoriesController> logger)
    {
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Yeni dataset kategorisi oluşturur
    /// </summary>
    /// <param name="dto">Kategori bilgileri</param>
    /// <returns>Oluşturulan kategori</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DatasetCategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateDatasetCategoryDto dto)
    {
        try
        {
            var result = await _categoryService.CreateAsync(dto);
            
            _logger.LogInformation(
                "Dataset category created: {CategoryName} (ID: {DataId})",
                result.CategoryName, result.DataId);

            return CreatedAtAction(
                nameof(GetById),
                new { dataId = result.DataId },
                result);
        }
        catch (InvalidOperationException ex)
        {
            return this.ErrorResponse("/api/v1/dataset-categories", "INVALID_OPERATION", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, "/api/v1/dataset-categories", "CREATE_CATEGORY_FAILED", "Kategori oluşturulurken hata oluştu", _logger);
        }
    }

    /// <summary>
    /// Dataset kategorilerini listeler (sayfalı)
    /// </summary>
    /// <param name="pageNumber">Sayfa numarası (default: 1)</param>
    /// <param name="pageSize">Sayfa boyutu (default: 20, max: 100)</param>
    /// <param name="search">Arama terimi (kategori adı veya açıklama)</param>
    /// <returns>Sayfalı kategori listesi</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<DatasetCategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        try
        {
            var result = await _categoryService.GetAllAsync(pageNumber, pageSize, search);
            
            _logger.LogInformation(
                "Listed dataset categories: Page {Page}, Size {Size}, Search: {Search}, Total {Total}",
                pageNumber, pageSize, search ?? "None", result.TotalCount);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, "/api/v1/dataset-categories", "LIST_CATEGORIES_FAILED", "Kategoriler listelenirken hata oluştu", _logger);
        }
    }

    /// <summary>
    /// ID'ye göre dataset kategorisi getirir
    /// </summary>
    /// <param name="dataId">Kategori ID'si (__dataId)</param>
    /// <returns>Kategori detayı</returns>
    [HttpGet("{dataId}")]
    [ProducesResponseType(typeof(DatasetCategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(string dataId)
    {
        try
        {
            var result = await _categoryService.GetByIdAsync(dataId);

            if (result == null)
            {
                return this.ErrorResponse($"/api/v1/dataset-categories/{dataId}", "CATEGORY_NOT_FOUND", "Kategori bulunamadı", statusCode: 404);
            }

            _logger.LogInformation("Retrieved dataset category: {DataId}", dataId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/dataset-categories/{dataId}", "GET_CATEGORY_FAILED", "Kategori getirilirken hata oluştu", _logger);
        }
    }

    /// <summary>
    /// Dataset kategorisini günceller
    /// </summary>
    /// <param name="dataId">Kategori ID'si (__dataId)</param>
    /// <param name="dto">Güncellenecek alanlar</param>
    /// <returns>Güncellenmiş kategori</returns>
    [HttpPut("{dataId}")]
    [ProducesResponseType(typeof(DatasetCategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(string dataId, [FromBody] UpdateDatasetCategoryDto dto)
    {
        try
        {
            var result = await _categoryService.UpdateAsync(dataId, dto);
            
            _logger.LogInformation(
                "Dataset category updated: {DataId}",
                dataId);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            var path = $"/api/v1/dataset-categories/{dataId}";
            if (ex.Message.Contains("bulunamadı"))
            {
                return this.ErrorResponse(path, "CATEGORY_NOT_FOUND", ex.Message, statusCode: 404);
            }
            return this.ErrorResponse(path, "INVALID_OPERATION", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/dataset-categories/{dataId}", "UPDATE_CATEGORY_FAILED", "Kategori güncellenirken hata oluştu", _logger);
        }
    }

    /// <summary>
    /// Dataset kategorisini siler (hard delete + __deletedDatas backup)
    /// </summary>
    /// <param name="dataId">Kategori ID'si (__dataId)</param>
    /// <returns>Silme başarı durumu</returns>
    [HttpDelete("{dataId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string dataId)
    {
        try
        {
            var result = await _categoryService.DeleteAsync(dataId);

            if (!result)
            {
                return this.ErrorResponse($"/api/v1/dataset-categories/{dataId}", "CATEGORY_NOT_FOUND", "Kategori bulunamadı", statusCode: 404);
            }

            _logger.LogInformation("Dataset category deleted: {DataId}", dataId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/dataset-categories/{dataId}", "DELETE_CATEGORY_FAILED", "Kategori silinirken hata oluştu", _logger);
        }
    }

    /// <summary>
    /// Silinen dataset kategorisini geri yükler
    /// </summary>
    /// <param name="dataId">Kategori ID'si (__dataId)</param>
    /// <returns>Geri yüklenen kategori</returns>
    [HttpPost("{dataId}/restore")]
    [ProducesResponseType(typeof(DatasetCategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Restore(string dataId)
    {
        try
        {
            var result = await _categoryService.RestoreAsync(dataId);
            
            _logger.LogInformation(
                "Dataset category restored: {DataId}",
                dataId);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            var path = $"/api/v1/dataset-categories/{dataId}/restore";
            if (ex.Message.Contains("bulunamadı"))
            {
                return this.ErrorResponse(path, "CATEGORY_NOT_FOUND", ex.Message, statusCode: 404);
            }
            return this.ErrorResponse(path, "INVALID_OPERATION", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/dataset-categories/{dataId}/restore", "RESTORE_CATEGORY_FAILED", "Kategori geri yüklenirken hata oluştu", _logger);
        }
    }
}

