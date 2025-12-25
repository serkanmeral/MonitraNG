using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngHub.Application.Configuration;
using MngHub.Application.Services;
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

            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMQ.Host,
                Port = _settings.RabbitMQ.Port,
                UserName = _settings.RabbitMQ.Username,
                Password = _settings.RabbitMQ.Password,
                VirtualHost = _settings.RabbitMQ.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Ensure exchanges exist
            _channel.ExchangeDeclare(
                exchange: _settings.RabbitMQ.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // Also declare mngkeeper.events exchange (for user/group events)
            _channel.ExchangeDeclare(
                exchange: _settings.RabbitMQ.EventPublisherExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            _logger.LogInformation("RabbitMQ connected successfully. Exchanges: {Exchange1}, {Exchange2}", 
                _settings.RabbitMQ.ExchangeName, _settings.RabbitMQ.EventPublisherExchangeName);
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
                consumerChannel.QueueBind(
                    queue: queueName,
                    exchange: _settings.RabbitMQ.ExchangeName,
                    routingKey: routingKey);

                _logger.LogDebug("Bound queue {QueueName} to {Exchange} with routing key {RoutingKey}",
                    queueName, _settings.RabbitMQ.ExchangeName, routingKey);

                // Also bind to mngkeeper.events exchange (for user/group events)
                // Only bind domainId-based patterns to this exchange
                if (!routingKey.StartsWith("global.") && !routingKey.StartsWith("system.") && !routingKey.StartsWith("domain."))
                {
                    // This is likely a domainId-based pattern (e.g., "507f1f77bcf86cd799439011.*")
                    consumerChannel.QueueBind(
                        queue: queueName,
                        exchange: _settings.RabbitMQ.EventPublisherExchangeName,
                        routingKey: routingKey);

                    _logger.LogDebug("Bound queue {QueueName} to {Exchange} with routing key {RoutingKey}",
                        queueName, _settings.RabbitMQ.EventPublisherExchangeName, routingKey);
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
                    var message = JsonSerializer.Deserialize<object>(messageJson);
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation(
                        "RabbitMQ message received for SignalR client. RoutingKey: {RoutingKey}, ConnectionId: {ConnectionId}, MessageSize: {MessageSize} bytes",
                        routingKey, connectionId, body.Length);

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

