using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Api.Controllers;

/// <summary>
/// Validation API — DG HTTP validation'dan çağrılır.
/// POST /api/v1/validate/{dataset} — payload ile validation pipeline'ları çalıştırır.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/validate")]
public class ValidateController : ControllerBase
{
    private readonly IValidationPipelineService _validationService;
    private readonly ILogger<ValidateController> _logger;

    public ValidateController(
        IValidationPipelineService validationService,
        ILogger<ValidateController> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// Dataset için validation pipeline'ları çalıştırır.
    /// DG, create/update öncesi bu endpoint'e POST atar.
    /// </summary>
    /// <param name="datasetName">Dataset adı (örn. tm_issues).</param>
    /// <param name="payload">Validate edilecek veri (request body).</param>
    /// <param name="cancellationToken">İptal token'ı.</param>
    /// <returns>{ isValid: bool, errorMessage?: string }</returns>
    [HttpPost("{datasetName}")]
    [ProducesResponseType(typeof(ValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(
        [FromRoute] string datasetName,
        [FromBody] Dictionary<string, object> payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(datasetName))
            return BadRequest(new { isValid = false, errorMessage = "Dataset name is required" });

        if (payload == null)
            payload = new Dictionary<string, object>();

        // Domain: JWT claims (domain_name, domain_id) veya X-Domain-Name header (dev/test)
        var domainName = User.FindFirstValue("domain_name") ?? User.FindFirstValue("domain_id")
            ?? Request.Headers["X-Domain-Name"].FirstOrDefault() ?? "";
        if (string.IsNullOrEmpty(domainName))
        {
            _logger.LogWarning("Validate called without domain in JWT or X-Domain-Name header");
            return BadRequest(new { isValid = false, errorMessage = "Domain not found in token or X-Domain-Name header" });
        }

        var authHeader = Request.Headers.Authorization.FirstOrDefault();

        var result = await _validationService.ValidateAsync(
            datasetName,
            payload,
            domainName,
            authHeader,
            cancellationToken);

        return Ok(new ValidationResponse(result.IsValid, result.ErrorMessage));
    }
}

/// <summary>
/// DG HTTP validation response formatı.
/// </summary>
public record ValidationResponse(bool IsValid, string? ErrorMessage = null);
