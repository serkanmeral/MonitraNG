using System;
using System.Threading.Tasks;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// RabbitMQ Service for domain-based event publishing
    /// </summary>
    public interface IRabbitMqService
    {
        /// <summary>
        /// Connect to RabbitMQ server
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Ensure domain-specific exchange exists
        /// Exchange pattern: monitra.data.events.{domainName}
        /// </summary>
        Task EnsureExchangeAsync(string domainName);

        /// <summary>
        /// Publish data event with retry mechanism
        /// </summary>
        /// <param name="domainName">Domain name for exchange routing</param>
        /// <param name="routingKey">Routing key (e.g., dataset.@tasks.created)</param>
        /// <param name="eventPayload">Event payload object</param>
        /// <param name="retryCount">Number of retry attempts (default: 3)</param>
        Task PublishDataEventAsync(string domainName, string routingKey, object eventPayload, int retryCount = 3);

        /// <summary>
        /// Check if connected to RabbitMQ
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Disconnect from RabbitMQ
        /// </summary>
        Task DisconnectAsync();
    }
}

