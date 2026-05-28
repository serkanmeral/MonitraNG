namespace MngScheduler.Application.Interfaces;

/// <summary>
/// SW-3c — Keeper token → MngOperations work-item-schedules execute.
/// </summary>
public interface IWorkItemScheduleOrchestrationService
{
    Task<WorkItemScheduleOrchestrationResult> ExecuteScheduleAsync(
        string scheduleDataId,
        CancellationToken cancellationToken = default);
}

public sealed class WorkItemScheduleOrchestrationResult
{
    public bool IsSuccess { get; set; }

    public int HttpStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }

    public string? WorkItemId { get; set; }
}
