using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.WorkspaceFolders;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workspace-folders")]
[Authorize]
public sealed class WorkspaceFoldersController : ControllerBase
{
    private readonly IWorkspaceFolderService _folders;

    public WorkspaceFoldersController(IWorkspaceFolderService folders)
    {
        _folders = folders;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _folders.ListAsync(cancellationToken);
        return Ok(new { items });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceFolderRequest request, CancellationToken cancellationToken)
    {
        var created = await _folders.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("workspaces/{workspaceId}/move")]
    public async Task<IActionResult> MoveWorkspace(
        string workspaceId,
        [FromBody] MoveWorkspaceToFolderRequest request,
        CancellationToken cancellationToken)
    {
        await _folders.MoveWorkspaceAsync(workspaceId, request, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWorkspaceFolderRequest request, CancellationToken cancellationToken)
    {
        var updated = await _folders.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id}/move")]
    public async Task<IActionResult> Move(string id, [FromBody] MoveWorkspaceFolderRequest request, CancellationToken cancellationToken)
    {
        var moved = await _folders.MoveAsync(id, request, cancellationToken);
        return Ok(moved);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _folders.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
