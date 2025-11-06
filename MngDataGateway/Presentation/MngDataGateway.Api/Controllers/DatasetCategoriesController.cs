using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.DatasetCategory;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Api.Controllers;

[ApiController]
[Route("api/dataset-categories")]
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
            _logger.LogWarning(ex, "Failed to create category: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dataset category");
            return StatusCode(500, new { error = "Kategori oluşturulurken hata oluştu", details = ex.Message, innerException = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Dataset kategorilerini listeler (sayfalı)
    /// </summary>
    /// <param name="pageNumber">Sayfa numarası (default: 1)</param>
    /// <param name="pageSize">Sayfa boyutu (default: 20, max: 100)</param>
    /// <returns>Sayfalı kategori listesi</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<DatasetCategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _categoryService.GetAllAsync(pageNumber, pageSize);
            
            _logger.LogInformation(
                "Listed dataset categories: Page {Page}, Size {Size}, Total {Total}",
                pageNumber, pageSize, result.TotalCount);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing dataset categories");
            return StatusCode(500, new { error = "Kategoriler listelenirken hata oluştu", details = ex.Message, innerException = ex.InnerException?.Message });
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
                _logger.LogWarning("Dataset category not found: {DataId}", dataId);
                return NotFound(new { error = "Kategori bulunamadı" });
            }

            _logger.LogInformation("Retrieved dataset category: {DataId}", dataId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dataset category: {DataId}", dataId);
            return StatusCode(500, new { error = "Kategori getirilirken hata oluştu" });
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
            _logger.LogWarning(ex, "Failed to update category {DataId}: {Message}", dataId, ex.Message);
            
            if (ex.Message.Contains("bulunamadı"))
            {
                return NotFound(new { error = ex.Message });
            }
            
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dataset category: {DataId}", dataId);
            return StatusCode(500, new { error = "Kategori güncellenirken hata oluştu" });
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
                _logger.LogWarning("Dataset category not found for deletion: {DataId}", dataId);
                return NotFound(new { error = "Kategori bulunamadı" });
            }

            _logger.LogInformation("Dataset category deleted: {DataId}", dataId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting dataset category: {DataId}", dataId);
            return StatusCode(500, new { error = "Kategori silinirken hata oluştu" });
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
            _logger.LogWarning(ex, "Failed to restore category {DataId}: {Message}", dataId, ex.Message);
            
            if (ex.Message.Contains("bulunamadı"))
            {
                return NotFound(new { error = ex.Message });
            }
            
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring dataset category: {DataId}", dataId);
            return StatusCode(500, new { error = "Kategori geri yüklenirken hata oluştu" });
        }
    }
}

