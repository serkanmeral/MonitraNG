using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Services;

namespace MngWorkflow.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/hooks")]
public sealed class WorkflowHooksController(IWorkflowHookService hooks) : ControllerBase
{
    [HttpPost("resume/delay")]
    public async Task<IActionResult> ResumeDelay([FromBody] WorkflowDelayResumeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await hooks.ResumeDelayAsync(request, cancellationToken);
            return Accepted(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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

    [HttpPost("schedule/run")]
    public async Task<IActionResult> RunSchedule([FromBody] WorkflowScheduleRunRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await hooks.RunScheduleTriggerAsync(request, cancellationToken);
            return Accepted(result);
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
