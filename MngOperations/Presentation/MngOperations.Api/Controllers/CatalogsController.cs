using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

/// <summary>
/// Global katalog CRUD'u (states/priorities/types/fields). MO write-through:
/// DG'ye yazar ve aynı işlemde MO cache'ini günceller.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/catalogs")]
[Authorize]
public class CatalogsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("{source}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(string source, CancellationToken cancellationToken)
    {
        var items = await _catalogService.ListAsync(source, cancellationToken);
        return Ok(items);
    }

    [HttpPost("{source}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        string source,
        [FromBody] Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        var created = await _catalogService.CreateAsync(source, data, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{source}/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string source,
        string id,
        [FromBody] Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        var updated = await _catalogService.UpdateAsync(source, id, data, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{source}/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string source, string id, CancellationToken cancellationToken)
    {
        await _catalogService.DeleteAsync(source, id, cancellationToken);
        return NoContent();
    }
}
