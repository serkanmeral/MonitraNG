using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngLLM.Application.DTOs.Di;
using MngLLM.Application.Services;
using MngLLM.Domain.Exceptions;

namespace MngLLM.Api.Controllers;

/// <summary>
/// Document Intelligence AI endpoints. Accepts DI resource id (no file upload).
/// Returns JSON only; persistence is Workflow's responsibility later.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/di")]
[Authorize(Policy = "AllowAnonymousInDevelopment")]
[Produces("application/json")]
public sealed class DiAiController : ControllerBase
{
    private readonly IDiExtractService _extractService;
    private readonly ILogger<DiAiController> _logger;

    public DiAiController(IDiExtractService extractService, ILogger<DiAiController> logger)
    {
        _extractService = extractService;
        _logger = logger;
    }

    /// <summary>
    /// Auto-Extract: load DI file by id, map UBL XML → earsiv_fatura JSON.
    /// </summary>
    [HttpPost("extract")]
    [ProducesResponseType(typeof(EarsivFaturaExtractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExtractAsync(
        [FromBody] DiExtractRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var auth = Request.Headers.Authorization.ToString();
            var result = await _extractService.ExtractAsync(request, auth, cancellationToken);
            return Ok(result);
        }
        catch (DiExtractException ex)
        {
            _logger.LogWarning(ex, "DI extract failed for resource {ResourceId}", request?.ResourceId);
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected DI extract failure");
            return StatusCode(500, new { error = "Extract failed", message = ex.Message });
        }
    }
}
