using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Document Designer — parametreli şablon tanımları (D1-alpha).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/templates")]
[Authorize]
public sealed class DocumentTemplatesController : ControllerBase
{
    private readonly IDocumentTemplateService _templates;

    public DocumentTemplatesController(IDocumentTemplateService templates)
    {
        _templates = templates;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TemplateListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _templates.ListAsync(ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct) =>
        Ok(await _templates.GetByIdAsync(id, ct));

    [HttpPost("from-source")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFromSource(
        [FromBody] CreateTemplateFromSourceRequest request,
        CancellationToken ct)
    {
        var result = await _templates.CreateFromSourceAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpGet("source/{resourceId}/structure")]
    [ProducesResponseType(typeof(DocxStructureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSourceStructure(string resourceId, CancellationToken ct) =>
        Ok(await _templates.GetSourceStructureAsync(resourceId, ct));

    [HttpPut("{id}/parameters")]
    [ProducesResponseType(typeof(TemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateParameters(
        string id,
        [FromBody] UpdateTemplateParametersRequest request,
        CancellationToken ct) =>
        Ok(await _templates.UpdateParametersAsync(id, request, ct));
}
