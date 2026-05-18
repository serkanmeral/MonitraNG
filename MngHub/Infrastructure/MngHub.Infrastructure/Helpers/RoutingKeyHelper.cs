using MngHub.Domain.Constants;

namespace MngHub.Infrastructure.Helpers;

/// <summary>
/// Helper class for building routing key patterns for subscriptions
/// </summary>
public static class RoutingKeyHelper
{
    /// <summary>
    /// Build routing keys list for a SignalR connection
    /// Includes: global, system, domain patterns, and DataGateway support
    /// </summary>
    public static List<string> BuildRoutingKeysForConnection(
        string domainName,
        string? domainId = null)
    {
        var routingKeys = new List<string>
        {
            RoutingKeyPatterns.Global,
            RoutingKeyPatterns.System,
            RoutingKeyPatterns.GetDomainPattern(domainName)
        };

        // Add domainId-based pattern if available (for MngKeeper + MngDataGateway unified events)
        // Chat Room (cht_*): EventPublisher uses "{domainId}.{EventType}" on mngdatagateway.events — this pattern receives them.
        if (!string.IsNullOrEmpty(domainId))
        {
            routingKeys.Add(RoutingKeyPatterns.GetDomainPatternById(domainId));
        }

        // Add domainName-based pattern for DataGateway events
        // DataGateway uses domainName as domainId in routing keys (e.g., "meral.datacreatedevent")
        routingKeys.Add($"{domainName}.*");

        // Monitoring ingest notify: Reactor publishes monitoring.data.updated.{domainName} (throttled)
        routingKeys.Add($"monitoring.data.updated.{domainName}");

        return routingKeys;
    }
}

