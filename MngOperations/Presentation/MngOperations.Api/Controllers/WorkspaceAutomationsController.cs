using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.Automations;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workspace-automations")]
[Authorize]
public sealed class WorkspaceAutomationsController : ControllerBase
{
    private readonly IWorkspaceAutomationService _workspaceAutomationService;

    public WorkspaceAutomationsController(IWorkspaceAutomationService workspaceAutomationService)
    {
        _workspaceAutomationService = workspaceAutomationService;
    }

    /// <summary>
    /// Otomasyonu seçilen kaynak iş kaydına karşı simüle eder (önizleme veya çalıştırma).
    /// </summary>
    [HttpPost("{id}/simulate")]
    [ProducesResponseType(typeof(SimulateWorkspaceAutomationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Simulate(
        string id,
        [FromBody] SimulateWorkspaceAutomationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceAutomationService.SimulateAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
