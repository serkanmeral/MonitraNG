using Microsoft.Extensions.Logging;
using MngDataGateway.Application.Events;
using MngDataGateway.Application.Interfaces;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Infrastructure.Services;

/// <summary>
/// Event Publisher implementation for DataGateway events
/// Uses MngKeeper-style approach: single exchange with domain-based routing keys
/// Exchange: mngdatagateway.events
/// Routing Key: {domainId}.{eventType}
/// Domain Isolation: Each domain only receives events with their domainId in routing key
/// Example: "meral.datacreatedevent" - only meral domain consumers receive this
/// </summary>
public class EventPublisher : IEventPublisher
{
    private readonly IRabbitMqService _rabbitMqService;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(
        IRabbitMqService rabbitMqService,
        ILogger<EventPublisher> logger)
    {
        _rabbitMqService = rabbitMqService ?? throw new ArgumentNullException(nameof(rabbitMqService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publish event with automatic routing key generation
    /// Routing key format: {domainId}.{eventType}
    /// Example: "meral.datacreatedevent"
    /// </summary>
    public async Task PublishAsync<T>(T @event, string domainId) where T : class
    {
        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("Domain ID cannot be empty", nameof(domainId));

        var eventType = typeof(T).Name.ToLower();
        var routingKey = $"{domainId}.{eventType}";
        
        await PublishAsync(@event, domainId, routingKey);
    }

    /// <summary>
    /// Publish event with custom routing key
    /// </summary>
    public async Task PublishAsync<T>(T @event, string domainId, string routingKey) where T : class
    {
        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("Domain ID cannot be empty", nameof(domainId));

        if (string.IsNullOrWhiteSpace(routingKey))
            throw new ArgumentException("Routing key cannot be empty", nameof(routingKey));

        try
        {
            // Set event properties if it's a BaseDataEvent
            if (@event is BaseDataEvent baseEvent)
            {
                baseEvent.DomainId = domainId;
                baseEvent.Type = typeof(T).Name;
                baseEvent.Timestamp = DateTime.UtcNow;
            }

            // Publish to unified exchange (MngKeeper-style)
            await _rabbitMqService.PublishToUnifiedExchangeAsync(domainId, routingKey, @event);

            _logger.LogInformation(
                "Event published successfully: {EventType} for domain: {DomainId}, routing key: {RoutingKey}",
                typeof(T).Name, domainId, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish event: {EventType} for domain: {DomainId}, routing key: {RoutingKey}",
                typeof(T).Name, domainId, routingKey);
            throw;
        }
    }
}

