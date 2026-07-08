using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.CoverPages;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Paylaşımlı kapak sayfası kataloğu (D-BR2).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cover-pages")]
[Authorize]
public sealed class CoverPagesController : ControllerBase
{
    private readonly ICoverPageService _coverPages;
    private readonly ICoverPageEditorService _coverPageEditor;

    public CoverPagesController(ICoverPageService coverPages, ICoverPageEditorService coverPageEditor)
    {
        _coverPages = coverPages;
        _coverPageEditor = coverPageEditor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CoverPageListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        Ok(await _coverPages.ListAsync(activeOnly, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CoverPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct) =>
        Ok(await _coverPages.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(CoverPageDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCoverPageRequest request, CancellationToken ct)
    {
        var result = await _coverPages.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(CoverPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCoverPageRequest request, CancellationToken ct) =>
        Ok(await _coverPages.UpdateAsync(id, request, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _coverPages.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id}/design-session")]
    [ProducesResponseType(typeof(CoverPageDesignSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDesignSession(string id, CancellationToken ct) =>
        Ok(await _coverPageEditor.CreateDesignSessionAsync(id, ct));
}
