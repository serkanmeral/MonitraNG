using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Api.Helpers;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Dataset;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/datasets")]
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
            return this.ErrorResponse($"/api/v1/datasets", "INVALID_OPERATION", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, "/api/v1/datasets", "CREATE_DATASET_FAILED", "Dataset oluşturulurken hata oluştu", _logger);
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
            return this.HandleError(ex, "/api/v1/datasets", "LIST_DATASETS_FAILED", "Datasets listelenirken hata oluştu", _logger);
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
                return this.ErrorResponse($"/api/v1/datasets/{name}", "DATASET_NOT_FOUND", "Dataset bulunamadı", statusCode: 404);
            }

            _logger.LogInformation("Retrieved dataset: {Name}", name);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/datasets/{name}", "GET_DATASET_FAILED", "Dataset getirilirken hata oluştu", _logger);
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
            var path = $"/api/v1/datasets/{name}";
            if (ex.Message.Contains("bulunamadı"))
            {
                return this.ErrorResponse(path, "DATASET_NOT_FOUND", ex.Message, statusCode: 404);
            }
            return this.ErrorResponse(path, "INVALID_OPERATION", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/datasets/{name}", "UPDATE_DATASET_FAILED", "Dataset güncellenirken hata oluştu", _logger);
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
                return this.ErrorResponse($"/api/v1/datasets/{name}", "DATASET_NOT_FOUND", "Dataset bulunamadı", statusCode: 404);
            }

            _logger.LogInformation("Dataset deleted: {Name} (collection NOT deleted)", name);
            return NoContent();
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/datasets/{name}", "DELETE_DATASET_FAILED", "Dataset silinirken hata oluştu", _logger);
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
            var path = $"/api/v1/datasets/{name}/restore";
            if (ex.Message.Contains("bulunamadı"))
            {
                return this.ErrorResponse(path, "DATASET_NOT_FOUND", ex.Message, statusCode: 404);
            }
            return this.ErrorResponse(path, "INVALID_OPERATION", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, $"/api/v1/datasets/{name}/restore", "RESTORE_DATASET_FAILED", "Dataset geri yüklenirken hata oluştu", _logger);
        }
    }
}

