using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.Schedules;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/work-item-schedules")]
[Authorize]
public sealed class WorkItemSchedulesController : ControllerBase
{
    private readonly IWorkItemScheduleSyncService _syncService;
    private readonly IWorkItemScheduleExecuteService _executeService;
    private readonly ILogger<WorkItemSchedulesController> _logger;

    public WorkItemSchedulesController(
        IWorkItemScheduleSyncService syncService,
        IWorkItemScheduleExecuteService executeService,
        ILogger<WorkItemSchedulesController> logger)
    {
        _syncService = syncService;
        _executeService = executeService;
        _logger = logger;
    }

    /// <summary>
    /// DG schedule kaydını MngScheduler User Job ile senkronlar (SW-3b).
    /// </summary>
    [HttpPost("{id}/sync-scheduler")]
    [ProducesResponseType(typeof(WorkItemScheduleSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SyncScheduler(string id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Sync-scheduler requested for schedule {ScheduleId}", id);
        var result = await _syncService.SyncSchedulerJobAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Schedule silinmeden önce bağlı MngScheduler User Job'ı kaldırır.
    /// </summary>
    [HttpPost("{id}/unlink-scheduler")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkScheduler(string id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Unlink-scheduler requested for schedule {ScheduleId}", id);
        await _syncService.UnlinkSchedulerJobAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Schedule şablonundan work item oluşturur (SW-2). Scheduler cron ve «Şimdi çalıştır» hedefi.
    /// </summary>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(WorkItemScheduleExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Execute(string id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Execute schedule requested for {ScheduleId}", id);
        var result = await _executeService.ExecuteAsync(id, cancellationToken);

        if (string.Equals(result.Code, "ALREADY_EXISTS", StringComparison.Ordinal))
            return Ok(result);

        return CreatedAtAction(nameof(Execute), new { id }, result);
    }
}
