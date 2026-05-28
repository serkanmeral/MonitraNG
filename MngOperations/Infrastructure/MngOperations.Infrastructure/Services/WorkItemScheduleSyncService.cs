using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Schedules;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class WorkItemScheduleSyncService : IWorkItemScheduleSyncService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMngSchedulerClient _scheduler;
    private readonly IRequestContext _requestContext;
    private readonly WorkItemScheduleSettings _settings;
    private readonly ILogger<WorkItemScheduleSyncService> _logger;

    public WorkItemScheduleSyncService(
        IMngDataGatewayClient dg,
        IMngSchedulerClient scheduler,
        IRequestContext requestContext,
        IOptions<MngOperationsSettings> settings,
        ILogger<WorkItemScheduleSyncService> logger)
    {
        _dg = dg;
        _scheduler = scheduler;
        _requestContext = requestContext;
        _settings = settings.Value.WorkItemSchedule;
        _logger = logger;
    }

    public async Task<WorkItemScheduleSyncResponse> SyncSchedulerJobAsync(
        string scheduleId,
        CancellationToken cancellationToken = default)
    {
        RequireManager();
        var token = RequireBearerToken();
        var schedule = await LoadScheduleAsync(scheduleId, token, cancellationToken);

        var jobId = ResolveJobId(schedule, scheduleId);
        var job = BuildUserJob(schedule, scheduleId, jobId);

        var existingById = await _scheduler.GetUserJobAsync(jobId, token, cancellationToken);
        var created = false;
        var updated = false;

        if (existingById != null)
        {
            await _scheduler.UpdateUserJobAsync(job, token, cancellationToken);
            updated = true;
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
                    "Scheduler create conflict for schedule {ScheduleId}; job {JobId} already exists",
                    scheduleId,
                    jobId);
                updated = true;
            }
        }

        if (!string.Equals(schedule.SchedulerJobId, jobId, StringComparison.Ordinal))
        {
            var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["schedulerJobId"] = jobId
            };
            await _dg.UpdateAsync(OcDatasets.WorkItemSchedules, scheduleId, patch, token, cancellationToken);
        }

        _logger.LogInformation(
            "Synced schedule {ScheduleId} → scheduler job {JobId} (created={Created}, updated={Updated})",
            scheduleId,
            jobId,
            created,
            updated);

        return new WorkItemScheduleSyncResponse
        {
            ScheduleId = scheduleId,
            SchedulerJobId = jobId,
            Created = created,
            Updated = updated
        };
    }

    public async Task UnlinkSchedulerJobAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        RequireManager();
        var token = RequireBearerToken();
        var schedule = await LoadScheduleAsync(scheduleId, token, cancellationToken);

        if (string.IsNullOrWhiteSpace(schedule.SchedulerJobId))
            return;

        try
        {
            await _scheduler.DeleteUserJobAsync(schedule.SchedulerJobId, token, cancellationToken);
            _logger.LogInformation(
                "Deleted scheduler job {JobId} for schedule {ScheduleId}",
                schedule.SchedulerJobId,
                scheduleId);
        }
        catch (OperationCoreException ex) when (ex.StatusCode == 404)
        {
            _logger.LogDebug(
                "Scheduler job {JobId} already absent for schedule {ScheduleId}",
                schedule.SchedulerJobId,
                scheduleId);
        }
    }

    private async Task<WorkItemScheduleRecord> LoadScheduleAsync(
        string scheduleId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scheduleId))
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "scheduleId is required.",
                "scheduleId zorunludur.",
                400);
        }

        var schedule = await _dg.GetByIdAsync<WorkItemScheduleRecord>(
            OcDatasets.WorkItemSchedules,
            scheduleId,
            token,
            cancellationToken);

        if (schedule == null || string.IsNullOrWhiteSpace(schedule.DataId))
        {
            throw new OperationCoreException(
                "SCHEDULE_NOT_FOUND",
                "Work item schedule not found.",
                "Zamanlama kaydı bulunamadı.",
                404);
        }

        ValidateScheduleForSync(schedule);
        return schedule;
    }

    private void ValidateScheduleForSync(WorkItemScheduleRecord schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.Name))
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "Schedule name is required.",
                "Zamanlama adı zorunludur.",
                400);
        }

        if (string.IsNullOrWhiteSpace(schedule.CronExpression))
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "Schedule cronExpression is required.",
                "Cron ifadesi zorunludur.",
                400);
        }
    }

    private string ResolveJobId(WorkItemScheduleRecord schedule, string scheduleId)
    {
        if (!string.IsNullOrWhiteSpace(schedule.SchedulerJobId))
            return schedule.SchedulerJobId.Trim();

        var prefix = string.IsNullOrWhiteSpace(_settings.SchedulerJobIdPrefix)
            ? "oc-schedule-"
            : _settings.SchedulerJobIdPrefix;
        return $"{prefix}{scheduleId}";
    }

    private SchedulerUserJobDto BuildUserJob(WorkItemScheduleRecord schedule, string scheduleId, string jobId)
    {
        var template = _settings.ExecuteEndpointTemplate;
        if (string.IsNullOrWhiteSpace(template) || !template.Contains("{scheduleId}", StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "SCHEDULER_NOT_CONFIGURED",
                "WorkItemSchedule:ExecuteEndpointTemplate must contain {scheduleId}.",
                "ExecuteEndpointTemplate {scheduleId} içermelidir.",
                500);
        }

        var endpointUrl = template.Replace("{scheduleId}", scheduleId, StringComparison.Ordinal);
        var description = string.IsNullOrWhiteSpace(schedule.Description)
            ? $"OperationCore schedule {scheduleId}"
            : schedule.Description;

        return new SchedulerUserJobDto
        {
            JobId = jobId,
            JobType = 1,
            Name = schedule.Name!.Trim(),
            Description = description,
            CronExpression = schedule.CronExpression!.Trim(),
            EndpointUrl = endpointUrl,
            HttpMethod = "POST",
            Payload = "{}",
            IsActive = schedule.IsActive,
            TimeoutSeconds = 300
        };
    }

    private void RequireManager()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "FORBIDDEN",
            "Only domain managers can manage work item schedules.",
            "Zamanlanmış işleri yalnızca domain yöneticileri yönetebilir.",
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
