using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using System.Text.Json;

namespace MngKeeper.Infrastructure.Services
{
    public class EventPublisher : IEventPublisher
    {
        private readonly IRabbitMqService _rabbitMqService;
        private readonly ILogger<EventPublisher> _logger;
        private const string ExchangeName = "mngkeeper.events";

        public EventPublisher(IRabbitMqService rabbitMqService, ILogger<EventPublisher> logger)
        {
            _rabbitMqService = rabbitMqService;
            _logger = logger;
        }

        public async Task PublishAsync<T>(T @event, string domainId) where T : class
        {
            var eventType = typeof(T).Name.ToLower();
            var routingKey = $"{domainId}.{eventType}";
            await PublishAsync(@event, domainId, routingKey);
        }

        public async Task PublishAsync<T>(T @event, string domainId, string routingKey) where T : class
        {
            try
            {
                // Set event properties
                if (@event is BaseEvent baseEvent)
                {
                    baseEvent.DomainId = domainId;
                    baseEvent.Type = typeof(T).Name;
                }

                var message = JsonSerializer.Serialize(@event, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await _rabbitMqService.PublishAsync(ExchangeName, routingKey, message);

                _logger.LogInformation("Event published successfully: {EventType} for domain: {DomainId}, routing key: {RoutingKey}", 
                    typeof(T).Name, domainId, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish event: {EventType} for domain: {DomainId}", typeof(T).Name, domainId);
                throw;
            }
        }
    }
}
