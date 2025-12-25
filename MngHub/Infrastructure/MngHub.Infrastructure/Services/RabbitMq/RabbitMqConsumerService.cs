using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngHub.Application.Configuration;
using MngHub.Application.Services;
using MngHub.Infrastructure.Helpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace MngHub.Infrastructure.Services.RabbitMq;

public class RabbitMqConsumerService : IRabbitMqConsumer, IDisposable
{
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private readonly MngHubSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly Dictionary<string, List<string>> _subscriptions = new(); // connectionId -> routingKeys
    private readonly Dictionary<string, EventingBasicConsumer> _consumers = new(); // connectionId -> consumer
    private readonly Dictionary<string, string> _queueNames = new(); // connectionId -> queueName
    private readonly Dictionary<string, IModel> _consumerChannels = new(); // connectionId -> channel
    private readonly object _lockObject = new();
    private bool _disposed = false;

    public RabbitMqConsumerService(
        ILogger<RabbitMqConsumerService> logger,
        IOptions<MngHubSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task ConnectAsync()
    {
        if (_connection?.IsOpen == true)
        {
            _logger.LogDebug("RabbitMQ already connected");
            return;
        }

        try
        {
            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}",
                _settings.RabbitMQ.Host,
                _settings.RabbitMQ.Port);

            var factory = RabbitMqConnectionHelper.CreateConnectionFactory(_settings);
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Ensure exchanges exist using helper
            RabbitMqConnectionHelper.EnsureExchangesExist(
                _channel,
                new[]
                {
                    _settings.RabbitMQ.ExchangeName,
                    _settings.RabbitMQ.EventPublisherExchangeName,
                    _settings.RabbitMQ.DataGatewayExchangeName
                },
                _logger);

            _logger.LogInformation("RabbitMQ connected successfully. Exchanges: {Exchange1}, {Exchange2}, {Exchange3}", 
                _settings.RabbitMQ.ExchangeName, _settings.RabbitMQ.EventPublisherExchangeName, _settings.RabbitMQ.DataGatewayExchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    public async Task SubscribeAsync(
        string connectionId,
        List<string> routingKeys,
        Func<string, object, Task> messageHandler)
    {
        await ConnectAsync();

        if (_connection == null || !_connection.IsOpen)
        {
            throw new InvalidOperationException("RabbitMQ connection is not available");
        }

        lock (_lockObject)
        {
            if (_subscriptions.ContainsKey(connectionId))
            {
                _logger.LogWarning("Connection {ConnectionId} already subscribed", connectionId);
                return;
            }

            // Create dedicated channel for this consumer
            var consumerChannel = _connection.CreateModel();

            // Create queue for this connection (exclusive, auto-delete)
            var queueName = $"mnghub.{connectionId}";
            consumerChannel.QueueDeclare(
                queue: queueName,
                durable: false,
                exclusive: true,
                autoDelete: true);

            // Bind queue to exchanges with routing keys
            foreach (var routingKey in routingKeys)
            {
                // Bind to mng.topics exchange (for system/domain events)
                BindQueueToExchange(consumerChannel, queueName, _settings.RabbitMQ.ExchangeName, routingKey);

                // Also bind to mngkeeper.events exchange (for user/group events)
                // Only bind domainId-based patterns to this exchange
                if (IsDomainIdBasedPattern(routingKey))
                {
                    BindQueueToExchange(consumerChannel, queueName, _settings.RabbitMQ.EventPublisherExchangeName, routingKey);
                    
                    // Also bind to mngdatagateway.events exchange (for data events)
                    // Same domainId-based pattern works for DataGateway events
                    BindQueueToExchange(consumerChannel, queueName, _settings.RabbitMQ.DataGatewayExchangeName, routingKey);
                }
            }

            // Create consumer
            var consumer = new EventingBasicConsumer(consumerChannel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);
                    var message = MessageSerializationHelper.Deserialize(messageJson);
                    var routingKey = ea.RoutingKey;
                    var exchange = ea.Exchange;

                    // Console'a detaylı log yazdır
                    _logger.LogInformation(
                        "[RabbitMQ Consumer] Message received. Exchange: {Exchange}, RoutingKey: {RoutingKey}, ConnectionId: {ConnectionId}, MessageSize: {MessageSize} bytes, Message: {Message}",
                        exchange, routingKey, connectionId, body.Length, messageJson);

                    await messageHandler(routingKey, message!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message for connection {ConnectionId}", connectionId);
                }
            };

            consumerChannel.BasicConsume(
                queue: queueName,
                autoAck: true,
                consumer: consumer);

            _subscriptions[connectionId] = routingKeys;
            _consumers[connectionId] = consumer;
            _queueNames[connectionId] = queueName;
            _consumerChannels[connectionId] = consumerChannel;
        }

        _logger.LogInformation(
            "Subscribed connection {ConnectionId} to {Count} routing keys",
            connectionId, routingKeys.Count);
    }

    public async Task UnsubscribeAsync(string connectionId)
    {
        IModel? consumerChannel = null;

        lock (_lockObject)
        {
            if (!_subscriptions.ContainsKey(connectionId))
            {
                _logger.LogDebug("Connection {ConnectionId} is not subscribed", connectionId);
                return;
            }

            // Get consumer channel
            if (_consumerChannels.TryGetValue(connectionId, out var channel))
            {
                consumerChannel = channel;
            }

            // Queue will be auto-deleted when connection closes (exclusive, autoDelete: true)
            _subscriptions.Remove(connectionId);
            _consumers.Remove(connectionId);
            _queueNames.Remove(connectionId);
            _consumerChannels.Remove(connectionId);
        }

        // Close channel outside lock
        if (consumerChannel != null)
        {
            try
            {
                consumerChannel.Close();
                consumerChannel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing consumer channel for connection {ConnectionId}", connectionId);
            }
        }

        await Task.CompletedTask;
        _logger.LogInformation("Unsubscribed connection {ConnectionId}", connectionId);
    }

    public async Task UnsubscribeAllAsync(string connectionId)
    {
        await UnsubscribeAsync(connectionId);
    }

    public async Task<bool> IsSubscribedAsync(string connectionId, string routingKey)
    {
        await Task.CompletedTask;

        lock (_lockObject)
        {
            return _subscriptions.ContainsKey(connectionId) &&
                   _subscriptions[connectionId].Contains(routingKey);
        }
    }


    private void BindQueueToExchange(IModel channel, string queueName, string exchangeName, string routingKey)
    {
        channel.QueueBind(
            queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        _logger.LogInformation("Bound queue {QueueName} to {Exchange} with routing key {RoutingKey}",
            queueName, exchangeName, routingKey);
    }

    private static bool IsDomainIdBasedPattern(string routingKey)
    {
        // DomainId-based patterns don't start with global, system, or domain prefixes
        return !routingKey.StartsWith("global.") && 
               !routingKey.StartsWith("system.") && 
               !routingKey.StartsWith("domain.");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Close all consumer channels
        lock (_lockObject)
        {
            foreach (var channel in _consumerChannels.Values)
            {
                try
                {
                    channel.Close();
                    channel.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing consumer channel");
                }
            }
            _consumerChannels.Clear();
        }

        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();

        _disposed = true;
        _logger.LogInformation("RabbitMQ connection disposed");
    }
}

