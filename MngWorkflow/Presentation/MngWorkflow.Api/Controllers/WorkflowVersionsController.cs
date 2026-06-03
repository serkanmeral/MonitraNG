using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[Route("api/v1/versions")]
public sealed class WorkflowVersionsController : ControllerBase
{
    private readonly IWorkflowVersionService _versions;

    public WorkflowVersionsController(IWorkflowVersionService versions) => _versions = versions;

    [HttpGet("{versionId}")]
    public async Task<IActionResult> Get(string versionId, CancellationToken cancellationToken)
    {
        var version = await _versions.GetAsync(versionId, cancellationToken);
        return version == null ? NotFound() : Ok(version);
    }

    [HttpPut("{versionId}")]
    public async Task<IActionResult> UpdateDraft(string versionId, [FromBody] UpdateWorkflowVersionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var version = await _versions.UpdateDraftAsync(versionId, request, cancellationToken);
            return version == null ? NotFound() : Ok(version);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{versionId}/publish")]
    public async Task<IActionResult> Publish(string versionId, CancellationToken cancellationToken)
    {
        try
        {
            var version = await _versions.PublishAsync(versionId, cancellationToken);
            return Ok(version);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
