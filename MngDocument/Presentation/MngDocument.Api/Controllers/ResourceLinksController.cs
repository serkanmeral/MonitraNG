using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.ResourceLinks;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>
/// Document Intelligence ↔ diğer modül bağlantıları (Faz 2: OperationCore work item).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class ResourceLinksController : ControllerBase
{
    private readonly IResourceLinkService _links;

    public ResourceLinksController(IResourceLinkService links)
    {
        _links = links;
    }

    [HttpPost("resource-links")]
    [ProducesResponseType(typeof(ResourceLinkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateResourceLinkRequest request, CancellationToken ct)
    {
        var result = await _links.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetLinkedWorkItems), new { resourceId = result.ResourceId, version = "1.0" }, result);
    }

    [HttpDelete("resource-links/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _links.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("resources/{resourceId}/linked-work-items")]
    [ProducesResponseType(typeof(ResourceLinkListResult<LinkedWorkItemSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinkedWorkItems(string resourceId, CancellationToken ct) =>
        Ok(await _links.GetLinkedWorkItemsAsync(resourceId, ct));

    [HttpGet("work-items/{workItemId}/linked-resources")]
    [ProducesResponseType(typeof(ResourceLinkListResult<LinkedResourceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinkedResourcesForWorkItem(string workItemId, CancellationToken ct) =>
        Ok(await _links.GetLinkedResourcesForWorkItemAsync(workItemId, ct));

    [HttpGet("resources/{resourceId}/related-resources")]
    [ProducesResponseType(typeof(ResourceLinkListResult<LinkedResourceSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelatedResources(string resourceId, CancellationToken ct) =>
        Ok(await _links.GetRelatedResourcesAsync(resourceId, ct));
}
