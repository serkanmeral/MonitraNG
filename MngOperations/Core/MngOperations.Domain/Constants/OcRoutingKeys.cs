namespace MngOperations.Domain.Constants;

/// <summary>
/// RabbitMQ routing key segments for oc.events (Q11).
/// </summary>
public static class OcRoutingKeys
{
    public const string WorkItemCreated = "oc.workitem.created";
    public const string WorkItemUpdated = "oc.workitem.updated";
    public const string WorkItemTransitioned = "oc.workitem.transitioned";

    public static string ForDomain(string domainId, string eventSuffix) =>
        $"{domainId}.{eventSuffix}";
}
