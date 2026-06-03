namespace MngWorkflow.Domain.Enums;

public enum WorkflowInstanceStatus
{
    Running = 0,
    Waiting = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}
