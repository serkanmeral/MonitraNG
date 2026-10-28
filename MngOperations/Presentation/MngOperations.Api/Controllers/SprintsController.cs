using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.Sprints;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workspaces/{workspaceId}/sprints")]
[Authorize]
public sealed class SprintsController : ControllerBase
{
    private readonly ISprintService _sprints;

    public SprintsController(ISprintService sprints)
    {
        _sprints = sprints;
    }

    [HttpGet]
    public async Task<IActionResult> List(string workspaceId, CancellationToken cancellationToken)
    {
        var items = await _sprints.ListAsync(workspaceId, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        string workspaceId,
        [FromBody] CreateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _sprints.CreateAsync(workspaceId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{sprintId}")]
    public async Task<IActionResult> Update(
        string workspaceId,
        string sprintId,
        [FromBody] UpdateSprintRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _sprints.UpdateAsync(workspaceId, sprintId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{sprintId}/activate")]
    public async Task<IActionResult> Activate(string workspaceId, string sprintId, CancellationToken cancellationToken)
    {
        var updated = await _sprints.ActivateAsync(workspaceId, sprintId, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{sprintId}/close")]
    public async Task<IActionResult> Close(string workspaceId, string sprintId, CancellationToken cancellationToken)
    {
        var updated = await _sprints.CloseAsync(workspaceId, sprintId, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{sprintId}")]
    public async Task<IActionResult> Delete(string workspaceId, string sprintId, CancellationToken cancellationToken)
    {
        await _sprints.DeleteAsync(workspaceId, sprintId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{sprintId}/items")]
    public async Task<IActionResult> Items(string workspaceId, string sprintId, CancellationToken cancellationToken)
    {
        var items = await _sprints.ListItemsAsync(workspaceId, sprintId, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("membership")]
    public async Task<IActionResult> SetMembership(
        string workspaceId,
        [FromBody] SetSprintMembershipRequest request,
        CancellationToken cancellationToken)
    {
        await _sprints.SetMembershipAsync(workspaceId, request, cancellationToken);
        return NoContent();
    }
}
