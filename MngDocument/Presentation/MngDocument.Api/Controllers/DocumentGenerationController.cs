using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Generic document generation from templates + context profiles.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/generate")]
[Authorize]
public sealed class DocumentGenerationController : ControllerBase
{
    private readonly IDocumentGenerationService _generation;

    public DocumentGenerationController(IDocumentGenerationService generation)
    {
        _generation = generation;
    }

    [HttpPost]
    [ProducesResponseType(typeof(GenerateDocumentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate([FromBody] GenerateDocumentRequest request, CancellationToken ct)
    {
        var result = await _generation.GenerateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(DocumentGenerationStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(
        [FromQuery] string profileCode,
        [FromQuery] string contextId,
        CancellationToken ct) =>
        Ok(await _generation.GetStatusAsync(profileCode, contextId, ct));

    [HttpGet("preview")]
    [ProducesResponseType(typeof(DocumentGenerationPreviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        [FromQuery] string profileCode,
        [FromQuery] string contextId,
        CancellationToken ct) =>
        Ok(await _generation.PreviewAsync(profileCode, contextId, ct));

    [HttpGet("context-types")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentContextTypeDto>), StatusCodes.Status200OK)]
    public IActionResult ListContextTypes() =>
        Ok(_generation.ListContextTypes());

    [HttpGet("context-types/{type}")]
    [ProducesResponseType(typeof(DocumentContextTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetContextType(string type)
    {
        var def = _generation.GetContextType(type);
        return def is null ? NotFound() : Ok(def);
    }
}
