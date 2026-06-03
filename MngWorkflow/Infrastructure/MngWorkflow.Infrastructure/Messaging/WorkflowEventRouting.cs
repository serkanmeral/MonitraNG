namespace MngWorkflow.Infrastructure.Messaging;

public static class WorkflowEventRouting
{
    /// <summary>
    /// Topic routing key: {domainId}.{eventType...} → (domainId, oc.workitem.created)
    /// </summary>
    public static (string DomainId, string EventType)? ParseTopicRoutingKey(string routingKey)
    {
        if (string.IsNullOrWhiteSpace(routingKey))
            return null;

        var dot = routingKey.IndexOf('.');
        if (dot <= 0 || dot >= routingKey.Length - 1)
            return null;

        var eventType = NormalizeEventType(routingKey[(dot + 1)..]);
        return (routingKey[..dot], eventType);
    }

    /// <summary>
    /// alarm.raised.7 → alarm.raised (severity suffix strip)
    /// </summary>
    public static string NormalizeEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType) || !eventType.StartsWith("alarm.", StringComparison.Ordinal))
            return eventType;

        var lastDot = eventType.LastIndexOf('.');
        if (lastDot <= 0 || lastDot >= eventType.Length - 1)
            return eventType;

        return int.TryParse(eventType[(lastDot + 1)..], out _)
            ? eventType[..lastDot]
            : eventType;
    }
}
