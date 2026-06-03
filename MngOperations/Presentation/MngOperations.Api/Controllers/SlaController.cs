using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.Sla;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sla")]
[Authorize]
public sealed class SlaController : ControllerBase
{
    private readonly ISlaBreachScanService _breachScanService;
    private readonly ISlaBreachScanSyncService _breachScanSyncService;
    private readonly ILogger<SlaController> _logger;

    public SlaController(
        ISlaBreachScanService breachScanService,
        ISlaBreachScanSyncService breachScanSyncService,
        ILogger<SlaController> logger)
    {
        _breachScanService = breachScanService;
        _breachScanSyncService = breachScanSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Workspace'teki açık iş kayıtlarında SLA response/resolve ihlallerini tarar;
    /// eşleşen <c>op_rules</c> automation kurallarını tetikler.
    /// </summary>
    [HttpPost("scan-breaches")]
    [ProducesResponseType(typeof(SlaBreachScanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ScanBreaches(
        [FromQuery] string workspaceId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("SLA breach scan requested for workspace {WorkspaceId}", workspaceId);
        var result = await _breachScanService.ScanWorkspaceAsync(workspaceId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Workspace için MngScheduler User Job oluşturur/günceller (cron → scan-breaches).
    /// </summary>
    [HttpPost("sync-scheduler")]
    [ProducesResponseType(typeof(SlaBreachScanSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncScheduler(
        [FromQuery] string workspaceId,
        [FromBody] SlaBreachScanSyncRequest? request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("SLA breach scan sync-scheduler for workspace {WorkspaceId}", workspaceId);
        var result = await _breachScanSyncService.SyncSchedulerJobAsync(workspaceId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Workspace'e bağlı SLA breach scan scheduler job'ını kaldırır.
    /// </summary>
    [HttpPost("unlink-scheduler")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkScheduler(
        [FromQuery] string workspaceId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("SLA breach scan unlink-scheduler for workspace {WorkspaceId}", workspaceId);
        await _breachScanSyncService.UnlinkSchedulerJobAsync(workspaceId, cancellationToken);
        return NoContent();
    }
}
