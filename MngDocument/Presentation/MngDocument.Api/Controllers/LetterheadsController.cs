using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Paylaşımlı antet kataloğu (D-BR1).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/letterheads")]
[Authorize]
public sealed class LetterheadsController : ControllerBase
{
    private readonly ILetterheadService _letterheads;
    private readonly ILetterheadEditorService _letterheadEditor;

    public LetterheadsController(ILetterheadService letterheads, ILetterheadEditorService letterheadEditor)
    {
        _letterheads = letterheads;
        _letterheadEditor = letterheadEditor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(LetterheadListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        Ok(await _letterheads.ListAsync(activeOnly, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LetterheadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct) =>
        Ok(await _letterheads.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(LetterheadDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateLetterheadRequest request, CancellationToken ct)
    {
        var result = await _letterheads.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(LetterheadDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateLetterheadRequest request, CancellationToken ct) =>
        Ok(await _letterheads.UpdateAsync(id, request, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _letterheads.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id}/design-session")]
    [ProducesResponseType(typeof(LetterheadDesignSessionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDesignSession(string id, CancellationToken ct) =>
        Ok(await _letterheadEditor.CreateDesignSessionAsync(id, ct));
}
