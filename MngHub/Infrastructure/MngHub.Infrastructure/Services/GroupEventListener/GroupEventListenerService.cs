using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngHub.Application.Configuration;
using MngHub.Infrastructure.Helpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace MngHub.Infrastructure.Services.GroupEventListener;

/// <summary>
/// Background service that listens to group/user events from mngkeeper.events exchange
/// This service only logs events to console for monitoring purposes.
/// SignalR message broadcasting is handled by NotificationHub to avoid duplicate messages.
/// Uses wildcard pattern to match all routing keys (e.g., {domainId}.groupupdatedevent)
/// </summary>
public class GroupEventListenerService : BackgroundService
{
    private readonly ILogger<GroupEventListenerService> _logger;
    private readonly MngHubSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;
    private string? _queueName;
    private const string AllEventsRoutingKeyPattern = "#"; // Match all routing keys

    public GroupEventListenerService(
        ILogger<GroupEventListenerService> logger,
        IOptions<MngHubSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GroupEventListenerService starting...");

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        try
        {
            await ConnectAndSubscribeAsync(stoppingToken);
            _logger.LogInformation("GroupEventListenerService initialized and ready to receive group/user events");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GroupEventListenerService. Will retry...");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection?.IsOpen != true || _channel?.IsOpen != true)
                {
                    _logger.LogWarning("RabbitMQ connection lost. Reconnecting...");
                    await ConnectAndSubscribeAsync(stoppingToken);
                }
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GroupEventListenerService. Retrying in 10 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_connection?.IsOpen != true)
            {
                var factory = RabbitMqConnectionHelper.CreateConnectionFactory(_settings);
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Ensure mngkeeper.events exchange exists using helper
                RabbitMqConnectionHelper.EnsureExchangeExists(
                    _channel,
                    _settings.RabbitMQ.EventPublisherExchangeName,
                    _logger);

                _logger.LogDebug("RabbitMQ connected for group/user event listener");
            }

            if (string.IsNullOrEmpty(_queueName))
            {
                // Create a durable queue for group/user events
                _queueName = "mnghub.group.listener";
                _channel.QueueDeclare(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                // Bind queue to mngkeeper.events exchange with wildcard pattern
                _channel.QueueBind(
                    queue: _queueName,
                    exchange: _settings.RabbitMQ.EventPublisherExchangeName,
                    routingKey: AllEventsRoutingKeyPattern);

                _logger.LogInformation(
                    "Queue {QueueName} bound to exchange {Exchange} with routing key pattern {Pattern}",
                    _queueName, _settings.RabbitMQ.EventPublisherExchangeName, AllEventsRoutingKeyPattern);
            }

            if (_consumer == null)
            {
                _consumer = new EventingBasicConsumer(_channel);
                _consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);
                        var message = MessageSerializationHelper.Deserialize(messageJson);
                        var routingKey = ea.RoutingKey;

                        // Console'a event bilgisini yazdır
                        _logger.LogInformation(
                            "[Group/User Event] Exchange: {Exchange}, RoutingKey: {RoutingKey}, Message: {Message}",
                            _settings.RabbitMQ.EventPublisherExchangeName, routingKey, messageJson);

                        // Note: SignalR message broadcasting is handled by NotificationHub
                        // This service only logs events to console for monitoring purposes
                        // This prevents duplicate messages in UI

                        // Acknowledge message
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing group/user event message");
                        // Reject and requeue on error
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _channel.BasicConsume(
                    queue: _queueName,
                    autoAck: false, // Manual acknowledgment
                    consumer: _consumer);

                _logger.LogInformation("GroupEventListenerService consumer started and listening for events");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to RabbitMQ for group/user events");
            throw;
        }
    }

    public override void Dispose()
    {
        try
        {
            _consumer?.Model?.Close();
            _channel?.Close();
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing GroupEventListenerService resources");
        }
        finally
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}

