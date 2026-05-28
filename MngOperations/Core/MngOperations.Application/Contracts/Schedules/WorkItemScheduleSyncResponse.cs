namespace MngOperations.Application.Contracts.Schedules;

public sealed class WorkItemScheduleSyncResponse
{
    public string ScheduleId { get; set; } = string.Empty;
    public string SchedulerJobId { get; set; } = string.Empty;
    public bool Created { get; set; }
    public bool Updated { get; set; }
}
