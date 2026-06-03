namespace MngWorkflow.Domain.Constants;

public static class WorkflowNodeTypes
{
    public const string ManualTrigger = "manual.trigger";
    public const string If = "if";
    public const string HttpRequest = "http.request";
    public const string WriteLog = "write.log";
    public const string ApprovalWait = "approval.wait";
    public const string DelayWait = "delay.wait";
    public const string WorkItemCreate = "workitem.create";
    public const string WorkItemTransition = "workitem.transition";
    public const string WorkItemUpdate = "workitem.update";
    public const string ParallelFork = "parallel.fork";
    public const string ParallelJoin = "parallel.join";
    public const string EngineCommand = "engine.command";
    public const string BlockIp = "block.ip";
}
