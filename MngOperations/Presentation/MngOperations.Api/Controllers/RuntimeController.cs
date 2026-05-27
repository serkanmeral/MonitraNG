using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/runtime")]
[Authorize]
public class RuntimeController : ControllerBase
{
    private readonly IRuntimeContextService _runtimeContextService;

    public RuntimeController(IRuntimeContextService runtimeContextService)
    {
        _runtimeContextService = runtimeContextService;
    }

    [HttpGet("work-items/{id}/profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(string id, CancellationToken cancellationToken)
    {
        var context = await _runtimeContextService.GetProfileAsync(id, cancellationToken);
        return Ok(context);
    }

    [HttpGet("work-items/{id}/timeline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(
        string id,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var page = await _runtimeContextService.GetTimelineAsync(id, skip, take, cancellationToken);
        return Ok(page);
    }

    [HttpGet("work-items/{id}/state-segments")]
    [ProducesResponseType(typeof(StateSegmentsPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStateSegments(string id, CancellationToken cancellationToken)
    {
        var page = await _runtimeContextService.GetStateSegmentsAsync(id, cancellationToken);
        return Ok(page);
    }

    [HttpGet("boards/{boardId}")]
    [ProducesResponseType(typeof(BoardRuntimeContext), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBoard(string boardId, CancellationToken cancellationToken)
    {
        var context = await _runtimeContextService.GetBoardAsync(boardId, cancellationToken);
        return Ok(context);
    }

    [HttpGet("dashboards/{dashboardId}")]
    [ProducesResponseType(typeof(DashboardRuntimeContext), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(string dashboardId, CancellationToken cancellationToken)
    {
        var context = await _runtimeContextService.GetDashboardAsync(dashboardId, cancellationToken);
        return Ok(context);
    }

    [HttpGet("work-items/form")]
    [ProducesResponseType(typeof(FormRuntimeContext), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFormCreate(
        [FromQuery] string workspaceId,
        [FromQuery] string? formId = null,
        [FromQuery] string mode = "create",
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mode, "create", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { code = "FORM_MODE_INVALID", message = "Use work-items/{id}/form for edit mode." });
        }

        var context = await _runtimeContextService.GetFormCreateAsync(workspaceId, formId, cancellationToken);
        return Ok(context);
    }

    [HttpGet("work-items/{id}/form")]
    [ProducesResponseType(typeof(FormRuntimeContext), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFormEdit(
        string id,
        [FromQuery] string mode = "edit",
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { code = "FORM_MODE_INVALID", message = "Use work-items/form?mode=create for create mode." });
        }

        var context = await _runtimeContextService.GetFormEditAsync(id, cancellationToken);
        return Ok(context);
    }

    [HttpPost("queries/{queryKey}/execute")]
    [ProducesResponseType(typeof(QueryExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteQuery(
        string queryKey,
        [FromBody] ExecuteQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _runtimeContextService.ExecuteQueryAsync(queryKey, request, cancellationToken);
        return Ok(result);
    }
}
