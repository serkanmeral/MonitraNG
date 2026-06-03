namespace MngWorkflow.Domain.Constants;

public static class WorkflowTriggerTypes
{
    public const string Event = "event";
    public const string Schedule = "schedule";
}

public static class WorkflowEventExchanges
{
    public const string OcEvents = "oc.events";
    public const string Alarms = "mng.alarms";

    public const string InboundQueue = "workflow.event.inbound";

    /// <summary>OC work item olayları — {domainId}.oc.workitem.*</summary>
    public const string OcWorkItemRoutingPattern = "*.oc.workitem.*";

    /// <summary>Alarm olayları — {domainId}.alarm.{lifecycle}.{severity}</summary>
    public const string AlarmsRoutingPattern = "*.alarm.#";
}
