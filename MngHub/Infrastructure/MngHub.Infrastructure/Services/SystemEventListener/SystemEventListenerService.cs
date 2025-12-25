using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngHub.Application.Configuration;
using MngHub.Infrastructure.Helpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace MngHub.Infrastructure.Services.SystemEventListener;

/// <summary>
/// Background service that listens to system events (system.#) from RabbitMQ
/// This ensures system events like domain.created are logged even when no SignalR clients are connected
/// Uses system.# pattern to match all system.* routing keys (including multi-segment ones like system.mngkeeper.domain.created)
/// </summary>
public class SystemEventListenerService : BackgroundService
{
    private readonly ILogger<SystemEventListenerService> _logger;
    private readonly MngHubSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;
    private string? _queueName;
    private const string SystemRoutingKeyPattern = "system.#";

    public SystemEventListenerService(
        ILogger<SystemEventListenerService> logger,
        IOptions<MngHubSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemEventListenerService starting...");

        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        try
        {
            await ConnectAndSubscribeAsync(stoppingToken);
            _logger.LogInformation("SystemEventListenerService initialized and ready to receive system events");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SystemEventListenerService. Will retry...");
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
                _logger.LogError(ex, "Error in SystemEventListenerService. Retrying in 10 seconds...");
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

                // Ensure exchange exists using helper
                RabbitMqConnectionHelper.EnsureExchangeExists(
                    _channel,
                    _settings.RabbitMQ.ExchangeName,
                    _logger);

                _logger.LogDebug("RabbitMQ connected for system event listener");
            }

            if (string.IsNullOrEmpty(_queueName))
            {
                _queueName = "mnghub.system.listener";
                
                _channel!.QueueDeclare(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                try
                {
                    _channel.QueueUnbind(
                        queue: _queueName,
                        exchange: _settings.RabbitMQ.ExchangeName,
                        routingKey: SystemRoutingKeyPattern);
                }
                catch
                {
                    // Ignore if binding doesn't exist
                }

                _channel.QueueBind(
                    queue: _queueName,
                    exchange: _settings.RabbitMQ.ExchangeName,
                    routingKey: SystemRoutingKeyPattern);

                _logger.LogInformation("System event listener queue created and bound. Queue: {QueueName}, Pattern: {Pattern}, Exchange: {Exchange}", 
                    _queueName, SystemRoutingKeyPattern, _settings.RabbitMQ.ExchangeName);
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
                        var routingKey = ea.RoutingKey;

                        _logger.LogInformation(
                            "System event received. RoutingKey: {RoutingKey}, MessageSize: {Size} bytes",
                            routingKey, body.Length);

                        if (routingKey == "system.mngkeeper.domain.created")
                        {
                            await HandleDomainCreatedEventAsync(messageJson, routingKey);
                        }
                        else
                        {
                            _logger.LogDebug("System event received. RoutingKey: {RoutingKey}", routingKey);
                        }

                        _channel!.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing system event. RoutingKey: {RoutingKey}", ea.RoutingKey);
                        try
                        {
                            _channel!.BasicNack(ea.DeliveryTag, false, true);
                        }
                        catch (Exception nackEx)
                        {
                            _logger.LogError(nackEx, "Failed to NACK message");
                        }
                    }
                };

                _consumer.Shutdown += (model, ea) =>
                {
                    _logger.LogWarning("Consumer shutdown. ReplyCode: {ReplyCode}, ReplyText: {ReplyText}", 
                        ea.ReplyCode, ea.ReplyText);
                };

                _channel.BasicConsume(
                    queue: _queueName,
                    autoAck: false,
                    consumer: _consumer);

                _logger.LogInformation("System event listener started. Queue: {QueueName}, Pattern: {Pattern}", 
                    _queueName, SystemRoutingKeyPattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect and subscribe to system events");
            throw;
        }

        await Task.CompletedTask;
    }

    private async Task HandleDomainCreatedEventAsync(string messageJson, string routingKey)
    {
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;

            string? domainName = null;
            string? domainId = null;
            string? eventId = null;

            if (root.TryGetProperty("payload", out var payload))
            {
                if (payload.TryGetProperty("domainName", out var domainNameProp))
                    domainName = domainNameProp.GetString();
                if (payload.TryGetProperty("domainId", out var domainIdProp))
                    domainId = domainIdProp.GetString();
            }

            if (root.TryGetProperty("eventId", out var eventIdProp))
                eventId = eventIdProp.GetString();

            _logger.LogInformation(
                "Domain created event received. EventId: {EventId}, DomainName: {DomainName}, DomainId: {DomainId}, RoutingKey: {RoutingKey}",
                eventId ?? "unknown", domainName ?? "unknown", domainId ?? "unknown", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse domain created event payload. RoutingKey: {RoutingKey}", routingKey);
            _logger.LogInformation("Domain created event (raw): {Message}", messageJson);
        }

        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SystemEventListenerService stopping...");

        try
        {
            _consumer = null;

            if (_channel?.IsOpen == true)
            {
                _channel.Close();
                _channel.Dispose();
            }

            if (_connection?.IsOpen == true)
            {
                _connection.Close();
                _connection.Dispose();
            }

            _logger.LogInformation("SystemEventListenerService stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping SystemEventListenerService");
        }

        await base.StopAsync(cancellationToken);
    }
}

