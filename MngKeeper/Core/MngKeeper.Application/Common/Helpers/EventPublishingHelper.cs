using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Common.Helpers;

/// <summary>
/// Helper class for consistent event publishing across handlers
/// </summary>
public static class EventPublishingHelper
{
    /// <summary>
    /// Publishes an event safely, logging errors without failing the operation
    /// </summary>
    public static async Task PublishEventSafelyAsync<T>(
        IEventPublisher eventPublisher,
        ILogger logger,
        T @event,
        string domainId,
        string eventName,
        string? contextId = null) where T : class
    {
        try
        {
            await eventPublisher.PublishAsync(@event, domainId);
            logger.LogDebug("Event published successfully: {EventName}, DomainId: {DomainId}, ContextId: {ContextId}",
                eventName, domainId, contextId ?? "N/A");
        }
        catch (Exception ex)
        {
            // Log error but don't fail the operation
            logger.LogError(ex, "Failed to publish {EventName} for domain {DomainId}, ContextId: {ContextId}",
                eventName, domainId, contextId ?? "N/A");
        }
    }
}

