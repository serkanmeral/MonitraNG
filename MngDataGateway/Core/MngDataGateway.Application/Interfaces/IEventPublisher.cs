namespace MngDataGateway.Application.Interfaces;

/// <summary>
/// Event publisher interface for publishing events to RabbitMQ
/// Domain isolation: Routing key format {domainId}.{eventType}
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publish event with automatic routing key generation
    /// Routing key: {domainId}.{eventType}
    /// Example: "meral.datacreatedevent"
    /// </summary>
    /// <typeparam name="T">Event type (must inherit from BaseDataEvent)</typeparam>
    /// <param name="event">Event object</param>
    /// <param name="domainId">Domain ID for routing and isolation</param>
    Task PublishAsync<T>(T @event, string domainId) where T : class;

    /// <summary>
    /// Publish event with custom routing key
    /// </summary>
    /// <typeparam name="T">Event type (must inherit from BaseDataEvent)</typeparam>
    /// <param name="event">Event object</param>
    /// <param name="domainId">Domain ID for routing and isolation</param>
    /// <param name="routingKey">Custom routing key</param>
    Task PublishAsync<T>(T @event, string domainId, string routingKey) where T : class;
}

