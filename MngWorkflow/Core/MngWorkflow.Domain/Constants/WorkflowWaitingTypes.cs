namespace MngWorkflow.Domain.Constants;

public static class WorkflowWaitingTypes
{
    public const string Approval = "WaitingApproval";
    public const string Delay = "WaitingDelay";
    public const string Event = "WaitingEvent";
    public const string ManualResume = "WaitingManualResume";
}
