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

    [HttpPost("run")]
    [ProducesResponseType(typeof(GenerateDocumentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Run(
        [FromBody] DocumentGenerationRuntimeEnvelope envelope,
        CancellationToken ct)
    {
        var result = await _generation.RunGenerationAsync(envelope, ct);
        return StatusCode(StatusCodes.Status201Created, result);
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

    [HttpGet("producers")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentProducerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProducers(CancellationToken ct) =>
        Ok(await _generation.ListProducersAsync(ct));

    [HttpGet("producers/{code}")]
    [ProducesResponseType(typeof(DocumentProducerDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProducer(string code, CancellationToken ct)
    {
        var producer = await _generation.GetProducerAsync(code, ct);
        return producer is null ? NotFound() : Ok(producer);
    }

    [HttpGet("data-sources")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentDataSourceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListDataSources(CancellationToken ct) =>
        Ok(await _generation.ListDataSourcesAsync(ct));

    [HttpGet("data-sources/{code}")]
    [ProducesResponseType(typeof(DocumentDataSourceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDataSource(string code, CancellationToken ct)
    {
        var source = await _generation.GetDataSourceAsync(code, ct);
        return source is null ? NotFound() : Ok(source);
    }

    [HttpGet("context-types")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentContextTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListContextTypes(CancellationToken ct) =>
        Ok(await _generation.ListContextTypesAsync(ct));

    [HttpGet("context-types/{type}")]
    [ProducesResponseType(typeof(DocumentContextTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContextType(string type, CancellationToken ct)
    {
        var def = await _generation.GetContextTypeAsync(type, ct);
        return def is null ? NotFound() : Ok(def);
    }
}
