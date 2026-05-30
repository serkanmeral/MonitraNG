using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/work-items")]
[Authorize]
public class WorkItemsController : ControllerBase
{
    private readonly IWorkItemCommandService _workItemCommandService;
    private readonly ILogger<WorkItemsController> _logger;

    public WorkItemsController(
        IWorkItemCommandService workItemCommandService,
        ILogger<WorkItemsController> logger)
    {
        _workItemCommandService = workItemCommandService;
        _logger = logger;
    }

    /// <summary>
    /// Create work item (Faz 1 pipeline — metadata, key, DG persist, activity, oc.events).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateWorkItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWorkItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _workItemCommandService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = result.WorkItem.Id }, result);
    }

    /// <summary>
    /// Dış modül (monitoring, security, scheduler) kökenli oluşturma — correlationId ile idempotent.
    /// </summary>
    [HttpPost("from-origin")]
    [ProducesResponseType(typeof(CreateWorkItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CreateWorkItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateFromOrigin(
        [FromBody] CreateFromOriginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workItemCommandService.CreateFromOriginAsync(request, cancellationToken);

        if (string.Equals(result.Code, "ALREADY_EXISTS", StringComparison.Ordinal))
            return Ok(result);

        return CreatedAtAction(nameof(Create), new { id = result.WorkItem.Id }, result);
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(WorkItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(
        string id,
        [FromBody] PatchWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workItemCommandService.PatchAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/transitions/{transitionKey}")]
    [ProducesResponseType(typeof(TransitionWorkItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyTransition(
        string id,
        string transitionKey,
        [FromBody] TransitionWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workItemCommandService.ApplyTransitionAsync(id, transitionKey, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _workItemCommandService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        string id,
        [FromBody] AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workItemCommandService.AddCommentAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(AddComment), new { id, commentId = result.Id }, result);
    }
}
