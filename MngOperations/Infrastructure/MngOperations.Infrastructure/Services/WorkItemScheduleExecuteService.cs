using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Schedules;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class WorkItemScheduleExecuteService : IWorkItemScheduleExecuteService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IWorkItemCommandService _workItemCommand;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<WorkItemScheduleExecuteService> _logger;

    public WorkItemScheduleExecuteService(
        IMngDataGatewayClient dg,
        IWorkItemCommandService workItemCommand,
        IRequestContext requestContext,
        ILogger<WorkItemScheduleExecuteService> logger)
    {
        _dg = dg;
        _workItemCommand = workItemCommand;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<WorkItemScheduleExecuteResponse> ExecuteAsync(
        string scheduleId,
        CancellationToken cancellationToken = default)
    {
        RequireManagerOrAdmin();
        var token = RequireBearerToken();
        var schedule = await LoadScheduleAsync(scheduleId, token, cancellationToken);

        if (!schedule.IsActive)
        {
            throw new OperationCoreException(
                "SCHEDULE_INACTIVE",
                "Work item schedule is not active.",
                "Zamanlama pasif durumda.",
                400);
        }

        ValidateScheduleForExecute(schedule);

        var correlationId = $"{scheduleId}:{Guid.NewGuid():N}";
        var fromOrigin = BuildFromOriginRequest(schedule, scheduleId, correlationId);

        _logger.LogInformation(
            "Executing work item schedule {ScheduleId} correlationId={CorrelationId}",
            scheduleId,
            correlationId);

        var createResult = await _workItemCommand.CreateFromOriginAsync(fromOrigin, cancellationToken);
        var workItemId = createResult.WorkItem.Id;
        var executedAt = DateTime.UtcNow;

        var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["lastRunAt"] = executedAt,
            ["lastWorkItemId"] = workItemId
        };
        await _dg.UpdateAsync(OcDatasets.WorkItemSchedules, scheduleId, patch, token, cancellationToken);

        return new WorkItemScheduleExecuteResponse
        {
            ScheduleId = scheduleId,
            Code = createResult.Code ?? "CREATED",
            WorkItemId = workItemId,
            WorkItemKey = createResult.WorkItem.Key,
            ExecutedAtUtc = executedAt
        };
    }

    private static CreateFromOriginRequest BuildFromOriginRequest(
        WorkItemScheduleRecord schedule,
        string scheduleId,
        string correlationId)
    {
        return new CreateFromOriginRequest
        {
            WorkspaceId = schedule.WorkspaceId!,
            TypeId = schedule.TypeId!,
            Title = schedule.Title!,
            Description = schedule.TemplateDescription,
            BoardId = schedule.BoardId,
            Assignee = schedule.Assignee,
            PriorityId = schedule.PriorityId,
            Fields = schedule.Fields,
            InitialTransitionKey = schedule.InitialTransitionKey,
            Origin = new WorkItemOriginInput
            {
                SourceType = "scheduler",
                SourceSystem = "MngScheduler",
                SourceId = scheduleId,
                CorrelationId = correlationId
            }
        };
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

        return schedule;
    }

    private static void ValidateScheduleForExecute(WorkItemScheduleRecord schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.WorkspaceId))
            throw new OperationCoreException("VALIDATION_ERROR", "workspaceId is required.", "workspaceId zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(schedule.TypeId))
            throw new OperationCoreException("VALIDATION_ERROR", "typeId is required.", "typeId zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(schedule.Title))
            throw new OperationCoreException("VALIDATION_ERROR", "title is required.", "title zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(schedule.BoardId))
            throw new OperationCoreException("VALIDATION_ERROR", "boardId is required.", "boardId zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(schedule.Assignee))
            throw new OperationCoreException("VALIDATION_ERROR", "assignee is required.", "assignee zorunludur.", 400);
    }

    private void RequireManagerOrAdmin()
    {
        if (_requestContext.IsAdmin || _requestContext.IsManager)
            return;

        throw new OperationCoreException(
            "FORBIDDEN",
            "Only domain managers can execute work item schedules.",
            "Zamanlanmış işleri yalnızca domain yöneticileri çalıştırabilir.",
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
