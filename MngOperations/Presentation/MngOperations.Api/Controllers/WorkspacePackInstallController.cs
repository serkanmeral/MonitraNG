using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.WorkspacePacks;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workspaces/{workspaceId}/pack")]
[Authorize]
public sealed class WorkspacePackInstallController : ControllerBase
{
    private readonly IWorkspacePackInstallService _install;

    public WorkspacePackInstallController(IWorkspacePackInstallService install)
    {
        _install = install;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string workspaceId, CancellationToken cancellationToken)
    {
        var current = await _install.GetAsync(workspaceId, cancellationToken);
        if (current is null) return NotFound();
        return Ok(current);
    }

    [HttpPost]
    public async Task<IActionResult> Install(
        string workspaceId,
        [FromBody] InstallWorkspacePackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _install.InstallAsync(workspaceId, request, cancellationToken);
        return Ok(result);
    }
}
