using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Events;
using MngOperations.Application.Interfaces;
using MngOperations.Domain.Constants;
using RabbitMQ.Client;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Publishes to oc.events with routing {domainId}.oc.workitem.* (Keeper-style + Q11).
/// </summary>
public class OcEventPublisher : IOcEventPublisher, IAsyncDisposable
{
    private readonly ILogger<OcEventPublisher> _logger;
    private readonly MngOperationsSettings _settings;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public OcEventPublisher(ILogger<OcEventPublisher> logger, IOptions<MngOperationsSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task PublishWorkItemEventAsync(
        OcWorkItemEvent @event,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false)
    {
        try
        {
            await PublishInternalAsync(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish OC event for domain {DomainId}", @event.DomainId);
            if (throwOnFailure)
                throw;
        }
    }

    private async Task PublishInternalAsync(OcWorkItemEvent @event, CancellationToken cancellationToken)
    {
        await EnsureConnectedAsync(cancellationToken);

        var routingSuffix = @event.EventType switch
        {
            "created" => OcRoutingKeys.WorkItemCreated,
            "updated" => OcRoutingKeys.WorkItemUpdated,
            "transitioned" => OcRoutingKeys.WorkItemTransitioned,
            _ => @event.EventType.Contains('.') ? @event.EventType : $"oc.workitem.{@event.EventType}"
        };

        var routingKey = OcRoutingKeys.ForDomain(@event.DomainId, routingSuffix);
        var message = JsonSerializer.Serialize(@event, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var body = Encoding.UTF8.GetBytes(message);
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = @event.EventId.ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ContentType = "application/json",
            ContentEncoding = "utf-8"
        };

        await _channel!.BasicPublishAsync(
            exchange: _settings.RabbitMq.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published OC event {EventType} domain {DomainId} routing {RoutingKey}",
            @event.EventType,
            @event.DomainId,
            routingKey);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            return;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
                return;

            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMq.Host,
                Port = _settings.RabbitMq.Port,
                UserName = _settings.RabbitMq.Username,
                Password = _settings.RabbitMq.Password,
                VirtualHost = _settings.RabbitMq.VirtualHost,
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: _settings.RabbitMq.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.CloseAsync();
        if (_connection != null)
            await _connection.CloseAsync();
        _connectionLock.Dispose();
    }
}
