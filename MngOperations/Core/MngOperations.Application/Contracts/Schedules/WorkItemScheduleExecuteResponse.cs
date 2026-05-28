namespace MngOperations.Application.Contracts.Schedules;

public sealed class WorkItemScheduleExecuteResponse
{
    public string ScheduleId { get; set; } = string.Empty;
    public string Code { get; set; } = "CREATED";
    public string WorkItemId { get; set; } = string.Empty;
    public string WorkItemKey { get; set; } = string.Empty;
    public DateTime ExecutedAtUtc { get; set; }
}
