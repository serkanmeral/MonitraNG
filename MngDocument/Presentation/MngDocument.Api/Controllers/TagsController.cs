using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Tags;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Document Intelligence etiket kataloğu (D-TAGS).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tags")]
[Authorize]
public sealed class TagsController : ControllerBase
{
    private readonly ITagService _tags;

    public TagsController(ITagService tags) => _tags = tags;

    [HttpGet]
    [ProducesResponseType(typeof(TagListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool activeOnly = false,
        [FromQuery] string? kind = null,
        CancellationToken ct = default) =>
        Ok(await _tags.ListAsync(activeOnly, kind, ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct) =>
        Ok(await _tags.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken ct)
    {
        var result = await _tags.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id, version = "1.0" }, result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TagDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTagRequest request, CancellationToken ct) =>
        Ok(await _tags.UpdateAsync(id, request, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _tags.DeleteAsync(id, ct);
        return NoContent();
    }
}
