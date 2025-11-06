using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Dataset;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Api.Controllers;

[ApiController]
[Route("api/datasets")]
[Authorize]
[Produces("application/json")]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetService _datasetService;
    private readonly ILogger<DatasetsController> _logger;

    public DatasetsController(
        IDatasetService datasetService,
        ILogger<DatasetsController> logger)
    {
        _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Yeni dataset schema oluşturur
    /// </summary>
    /// <param name="dto">Dataset schema bilgileri</param>
    /// <returns>Oluşturulan dataset schema</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DatasetResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateDatasetDto dto)
    {
        try
        {
            var result = await _datasetService.CreateAsync(dto);
            
            _logger.LogInformation(
                "Dataset schema created: {Name} (ID: {DataId})",
                result.Name, result.DataId);

            return CreatedAtAction(
                nameof(GetByName),
                new { name = result.Name },
                result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create dataset: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dataset schema");
            return StatusCode(500, new { error = "Dataset oluşturulurken hata oluştu", details = ex.Message });
        }
    }

    /// <summary>
    /// Dataset schema'larını listeler (sayfalı)
    /// </summary>
    /// <param name="pageNumber">Sayfa numarası (default: 1)</param>
    /// <param name="pageSize">Sayfa boyutu (default: 20, max: 100)</param>
    /// <returns>Sayfalı dataset schema listesi</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<DatasetResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _datasetService.GetAllAsync(pageNumber, pageSize);
            
            _logger.LogInformation(
                "Listed datasets: Page {Page}, Size {Size}, Total {Total}",
                pageNumber, pageSize, result.TotalCount);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing datasets");
            return StatusCode(500, new { error = "Datasets listelenirken hata oluştu", details = ex.Message });
        }
    }

    /// <summary>
    /// Name'e göre dataset schema getirir
    /// </summary>
    /// <param name="name">Dataset name (örn: @tasks)</param>
    /// <returns>Dataset schema detayı</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(DatasetResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetByName(string name)
    {
        try
        {
            var result = await _datasetService.GetByNameAsync(name);

            if (result == null)
            {
                _logger.LogWarning("Dataset not found: {Name}", name);
                return NotFound(new { error = "Dataset bulunamadı" });
            }

            _logger.LogInformation("Retrieved dataset: {Name}", name);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dataset: {Name}", name);
            return StatusCode(500, new { error = "Dataset getirilirken hata oluştu", details = ex.Message });
        }
    }

    /// <summary>
    /// Dataset schema'yı günceller
    /// </summary>
    /// <param name="name">Dataset name</param>
    /// <param name="dto">Güncellenecek alanlar</param>
    /// <returns>Güncellenmiş dataset schema</returns>
    [HttpPut("{name}")]
    [ProducesResponseType(typeof(DatasetResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(string name, [FromBody] UpdateDatasetDto dto)
    {
        try
        {
            var result = await _datasetService.UpdateAsync(name, dto);
            
            _logger.LogInformation("Dataset updated: {Name}", name);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to update dataset {Name}: {Message}", name, ex.Message);
            
            if (ex.Message.Contains("bulunamadı"))
            {
                return NotFound(new { error = ex.Message });
            }
            
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating dataset: {Name}", name);
            return StatusCode(500, new { error = "Dataset güncellenirken hata oluştu", details = ex.Message });
        }
    }

    /// <summary>
    /// Dataset schema'yı siler (hard delete + __deletedDatas backup)
    /// </summary>
    /// <param name="name">Dataset name</param>
    /// <returns>Silme başarı durumu</returns>
    /// <remarks>
    /// NOTE: Sadece schema metadata silinir. 
    /// Collection (@tasks, @users) ve data silinmez!
    /// </remarks>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string name)
    {
        try
        {
            var result = await _datasetService.DeleteAsync(name);

            if (!result)
            {
                _logger.LogWarning("Dataset not found for deletion: {Name}", name);
                return NotFound(new { error = "Dataset bulunamadı" });
            }

            _logger.LogInformation("Dataset deleted: {Name} (collection NOT deleted)", name);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting dataset: {Name}", name);
            return StatusCode(500, new { error = "Dataset silinirken hata oluştu", details = ex.Message });
        }
    }

    /// <summary>
    /// Silinen dataset schema'yı geri yükler
    /// </summary>
    /// <param name="name">Dataset name</param>
    /// <returns>Geri yüklenen dataset schema</returns>
    [HttpPost("{name}/restore")]
    [ProducesResponseType(typeof(DatasetResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Restore(string name)
    {
        try
        {
            var result = await _datasetService.RestoreAsync(name);
            
            _logger.LogInformation("Dataset restored: {Name}", name);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to restore dataset {Name}: {Message}", name, ex.Message);
            
            if (ex.Message.Contains("bulunamadı"))
            {
                return NotFound(new { error = ex.Message });
            }
            
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring dataset: {Name}", name);
            return StatusCode(500, new { error = "Dataset geri yüklenirken hata oluştu", details = ex.Message });
        }
    }
}

