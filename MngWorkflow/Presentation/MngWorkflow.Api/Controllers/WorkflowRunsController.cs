using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[Route("api/v1/runs")]
public sealed class WorkflowRunsController : ControllerBase
{
    private readonly IWorkflowRunService _runs;

    public WorkflowRunsController(IWorkflowRunService runs) => _runs = runs;

    [HttpPost]
    public async Task<IActionResult> Start([FromBody] StartWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runs.StartRunAsync(request, cancellationToken);
            return Accepted(new
            {
                result.InstanceId,
                result.CorrelationId,
                result.WorkflowVersionId,
                result.EntryNodeId,
                status = "queued"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? workflowId,
        [FromQuery] WorkflowInstanceStatus? status,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _runs.ListRunsAsync(new WorkflowRunHistoryQuery
        {
            WorkflowId = workflowId,
            Status = status,
            Skip = skip,
            Limit = limit
        }, cancellationToken));

    [HttpGet("{instanceId}")]
    public async Task<IActionResult> Get(string instanceId, CancellationToken cancellationToken)
    {
        var detail = await _runs.GetRunDetailAsync(instanceId, cancellationToken);
        return detail == null ? NotFound() : Ok(detail);
    }
}
