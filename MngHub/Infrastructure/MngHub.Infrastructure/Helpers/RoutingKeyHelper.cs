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

        // Add domainId-based pattern if available (for MngKeeper events)
        if (!string.IsNullOrEmpty(domainId))
        {
            routingKeys.Add(RoutingKeyPatterns.GetDomainPatternById(domainId));
        }

        // Add domainName-based pattern for DataGateway events
        // DataGateway uses domainName as domainId in routing keys (e.g., "meral.datacreatedevent")
        routingKeys.Add($"{domainName}.*");

        return routingKeys;
    }
}

