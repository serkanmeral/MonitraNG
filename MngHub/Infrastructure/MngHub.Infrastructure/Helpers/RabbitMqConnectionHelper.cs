using Microsoft.Extensions.Logging;
using MngHub.Application.Configuration;
using RabbitMQ.Client;

namespace MngHub.Infrastructure.Helpers;

/// <summary>
/// Helper class for creating RabbitMQ connections
/// </summary>
public static class RabbitMqConnectionHelper
{
    /// <summary>
    /// Create a RabbitMQ ConnectionFactory with standard settings
    /// </summary>
    public static ConnectionFactory CreateConnectionFactory(MngHubSettings settings)
    {
        return new ConnectionFactory
        {
            HostName = settings.RabbitMQ.Host,
            Port = settings.RabbitMQ.Port,
            UserName = settings.RabbitMQ.Username,
            Password = settings.RabbitMQ.Password,
            VirtualHost = settings.RabbitMQ.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        };
    }

    /// <summary>
    /// Ensure exchange exists (idempotent operation)
    /// </summary>
    public static void EnsureExchangeExists(
        IModel channel,
        string exchangeName,
        ILogger? logger = null)
    {
        if (channel == null)
            throw new ArgumentNullException(nameof(channel));

        if (string.IsNullOrWhiteSpace(exchangeName))
            throw new ArgumentException("Exchange name cannot be empty", nameof(exchangeName));

        try
        {
            channel.ExchangeDeclare(
                exchange: exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            logger?.LogDebug("Exchange {ExchangeName} declared/verified", exchangeName);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to declare exchange {ExchangeName}", exchangeName);
            throw;
        }
    }

    /// <summary>
    /// Ensure multiple exchanges exist
    /// </summary>
    public static void EnsureExchangesExist(
        IModel channel,
        IEnumerable<string> exchangeNames,
        ILogger? logger = null)
    {
        foreach (var exchangeName in exchangeNames)
        {
            EnsureExchangeExists(channel, exchangeName, logger);
        }
    }
}

