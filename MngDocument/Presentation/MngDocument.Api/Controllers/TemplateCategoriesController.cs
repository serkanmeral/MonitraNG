using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Document Designer — kategori katalog ağacı (D1-beta).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/template-categories")]
[Authorize]
public sealed class TemplateCategoriesController : ControllerBase
{
    private readonly ITemplateCategoryService _categories;

    public TemplateCategoriesController(ITemplateCategoryService categories)
    {
        _categories = categories;
    }

    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<TemplateCategoryTreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken ct) =>
        Ok(await _categories.GetTreeAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(TemplateCategoryDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateCategoryRequest request, CancellationToken ct)
    {
        var created = await _categories.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetTree), new { version = "1.0" }, created);
    }

    [HttpPut("{id}/rename")]
    [ProducesResponseType(typeof(TemplateCategoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Rename(string id, [FromBody] RenameTemplateCategoryRequest request, CancellationToken ct) =>
        Ok(await _categories.RenameAsync(id, request, ct));

    [HttpPut("{id}/move")]
    [ProducesResponseType(typeof(TemplateCategoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Move(string id, [FromBody] MoveTemplateCategoryRequest request, CancellationToken ct) =>
        Ok(await _categories.MoveAsync(id, request, ct));

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
