using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Schedules;
using MngOperations.Application.Contracts.Sla;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class SlaBreachScanSyncService : ISlaBreachScanSyncService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMngSchedulerClient _scheduler;
    private readonly IRequestContext _requestContext;
    private readonly SlaBreachScanSettings _settings;
    private readonly ILogger<SlaBreachScanSyncService> _logger;

    public SlaBreachScanSyncService(
        IMngDataGatewayClient dg,
        IMngSchedulerClient scheduler,
        IRequestContext requestContext,
        IOptions<MngOperationsSettings> settings,
        ILogger<SlaBreachScanSyncService> logger)
    {
        _dg = dg;
        _scheduler = scheduler;
        _requestContext = requestContext;
        _settings = settings.Value.SlaBreachScan;
        _logger = logger;
    }

    public async Task<SlaBreachScanSyncResponse> SyncSchedulerJobAsync(
        string workspaceId,
        SlaBreachScanSyncRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        RequireManager();
        var token = RequireBearerToken();
        var workspace = await LoadWorkspaceAsync(workspaceId, token, cancellationToken);

        var cron = request?.CronExpression?.Trim();
        if (string.IsNullOrWhiteSpace(cron))
            cron = workspace.SlaBreachScanCronExpression?.Trim();
        if (string.IsNullOrWhiteSpace(cron))
            cron = _settings.DefaultCronExpression;

        var isActive = request?.IsActive ?? workspace.SlaBreachScanEnabled ?? true;
        var jobId = ResolveJobId(workspace, workspaceId);
        var job = BuildUserJob(workspace, workspaceId, jobId, cron, isActive);

        var existingById = await _scheduler.GetUserJobAsync(jobId, token, cancellationToken);
        var created = false;
        var updated = false;

        if (existingById != null)
        {
            try
            {
                await _scheduler.UpdateUserJobAsync(job, token, cancellationToken);
                updated = true;
            }
            catch (OperationCoreException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Scheduler update failed for SLA scan job {JobId}; recreating",
                    jobId);
                try
                {
                    await _scheduler.DeleteUserJobAsync(jobId, token, cancellationToken);
                }
                catch (OperationCoreException deleteEx)
                {
                    _logger.LogDebug(deleteEx, "Scheduler delete skipped for job {JobId} during recreate", jobId);
                }

                await _scheduler.CreateUserJobAsync(job, token, cancellationToken);
                created = true;
                updated = false;
            }
        }
        else
        {
            try
            {
                await _scheduler.CreateUserJobAsync(job, token, cancellationToken);
                created = true;
            }
            catch (OperationCoreException ex) when (ex.StatusCode is 400 or 409)
            {
                var retry = await _scheduler.GetUserJobAsync(jobId, token, cancellationToken);
                if (retry == null)
                    throw;

                _logger.LogWarning(
                    "Scheduler create conflict for SLA scan workspace {WorkspaceId}; job {JobId} already exists",
                    workspaceId,
                    jobId);
                updated = true;
            }
        }

        var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["slaBreachScanSchedulerJobId"] = jobId,
            ["slaBreachScanCronExpression"] = cron,
            ["slaBreachScanEnabled"] = isActive
        };
        await _dg.UpdateAsync(OcDatasets.Workspaces, workspaceId, patch, token, cancellationToken);

        _logger.LogInformation(
            "Synced SLA breach scan for workspace {WorkspaceId} → scheduler job {JobId} (created={Created}, updated={Updated})",
            workspaceId,
            jobId,
            created,
            updated);

        return new SlaBreachScanSyncResponse
        {
            WorkspaceId = workspaceId.Trim(),
            SchedulerJobId = jobId,
            CronExpression = cron,
            Created = created,
            Updated = updated
        };
    }

    public async Task UnlinkSchedulerJobAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        RequireManager();
        var token = RequireBearerToken();
        var workspace = await LoadWorkspaceAsync(workspaceId, token, cancellationToken);

        var jobId = workspace.SlaBreachScanSchedulerJobId;
        if (string.IsNullOrWhiteSpace(jobId))
            jobId = ResolveJobId(workspace, workspaceId);

        if (string.IsNullOrWhiteSpace(workspace.SlaBreachScanSchedulerJobId))
            return;

        try
        {
            await _scheduler.DeleteUserJobAsync(jobId!, token, cancellationToken);
            _logger.LogInformation(
                "Deleted SLA breach scan scheduler job {JobId} for workspace {WorkspaceId}",
                jobId,
                workspaceId);
        }
        catch (OperationCoreException ex) when (ex.StatusCode == 404)
        {
            _logger.LogDebug(
                "SLA breach scan scheduler job {JobId} already absent for workspace {WorkspaceId}",
                jobId,
                workspaceId);
        }

        var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["slaBreachScanSchedulerJobId"] = null,
            ["slaBreachScanEnabled"] = false
        };
        await _dg.UpdateAsync(OcDatasets.Workspaces, workspaceId, patch, token, cancellationToken);
    }

    private async Task<WorkspaceRecord> LoadWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "workspaceId is required.",
                "workspaceId zorunludur.",
                400);
        }

        var workspace = await _dg.GetByIdAsync<WorkspaceRecord>(
            OcDatasets.Workspaces,
            workspaceId.Trim(),
            token,
            cancellationToken);

        if (workspace == null || string.IsNullOrWhiteSpace(workspace.DataId))
        {
            throw new OperationCoreException(
                "WORKSPACE_NOT_FOUND",
                "Workspace not found.",
                "Workspace bulunamadı.",
                404);
        }

        return workspace;
    }

    private string ResolveJobId(WorkspaceRecord workspace, string workspaceId)
    {
        if (!string.IsNullOrWhiteSpace(workspace.SlaBreachScanSchedulerJobId))
            return workspace.SlaBreachScanSchedulerJobId.Trim();

        var prefix = string.IsNullOrWhiteSpace(_settings.SchedulerJobIdPrefix)
            ? "oc-sla-scan-"
            : _settings.SchedulerJobIdPrefix;
        return $"{prefix}{workspaceId.Trim()}";
    }

    private SchedulerUserJobDto BuildUserJob(
        WorkspaceRecord workspace,
        string workspaceId,
        string jobId,
        string cronExpression,
        bool isActive)
    {
        var template = _settings.ScanEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template) || !template.Contains("{workspaceId}", StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "SCHEDULER_NOT_CONFIGURED",
                "SlaBreachScan:ScanEndpointTemplate must contain {workspaceId}.",
                "ScanEndpointTemplate {workspaceId} içermelidir.",
                500);
        }

        var endpointUrl = template.Replace("{workspaceId}", Uri.EscapeDataString(workspaceId.Trim()), StringComparison.Ordinal);
        var label = string.IsNullOrWhiteSpace(workspace.Name)
            ? workspace.Key ?? workspaceId
            : workspace.Name;

        return new SchedulerUserJobDto
        {
            JobId = jobId,
            JobType = 1,
            Name = $"{label} SLA breach scan",
            Description = $"OperationCore SLA breach scan for workspace {workspaceId}",
            CronExpression = cronExpression,
            EndpointUrl = endpointUrl,
            HttpMethod = "POST",
            Payload = "{}",
            IsActive = isActive,
            TimeoutSeconds = 300
        };
    }

    private void RequireManager()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "FORBIDDEN",
            "Only domain managers can manage SLA breach scan schedules.",
            "SLA ihlali taramasını yalnızca domain yöneticileri yönetebilir.",
            403);
    }

    private string RequireBearerToken()
    {
        if (string.IsNullOrWhiteSpace(_requestContext.BearerToken))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "Bearer token is required.",
                "Bearer token gerekli.",
                401);
        }

        return _requestContext.BearerToken;
    }
}
