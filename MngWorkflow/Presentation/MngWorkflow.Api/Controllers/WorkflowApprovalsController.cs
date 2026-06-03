using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[Route("api/v1/approvals")]
public sealed class WorkflowApprovalsController(IWorkflowApprovalService approvals) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] WorkflowApprovalStatus? status,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await approvals.ListAsync(status, skip, limit, cancellationToken));

    [HttpGet("{approvalId}")]
    public async Task<IActionResult> Get(string approvalId, CancellationToken cancellationToken)
    {
        var item = await approvals.GetAsync(approvalId, cancellationToken);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost("{approvalId}/decide")]
    public async Task<IActionResult> Decide(
        string approvalId,
        [FromBody] DecideWorkflowApprovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await approvals.DecideAsync(approvalId, request, cancellationToken);
            return Accepted(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/v1/secrets")]
public sealed class WorkflowSecretsController(IWorkflowSecretService secrets) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await secrets.ListAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] CreateWorkflowSecretRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await secrets.UpsertAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
