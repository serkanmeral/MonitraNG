using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Observations;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Services.Ingest;
using RabbitMQ.Client;

namespace MngLogCollector.Infrastructure.Messaging;

public sealed class AgentObservationPublisher : IAgentObservationPublisher, IDisposable
{
    private readonly ILogger<AgentObservationPublisher> _logger;
    private readonly RabbitMqSettings _rabbit;
    private readonly ObservationPublishSettings _publish;
    private readonly object _channelLock = new();
    private IConnection? _connection;
    private IModel? _channel;

    public AgentObservationPublisher(
        ILogger<AgentObservationPublisher> logger,
        IOptions<MngLogCollectorSettings> options)
    {
        _logger = logger;
        _rabbit = options.Value.RabbitMq;
        _publish = options.Value.ObservationPublish;
    }

    public Task PublishEventAsync(
        AgentObservationPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!_publish.Enabled)
            return Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(payload.DomainName) || string.IsNullOrWhiteSpace(payload.Key))
            return Task.CompletedTask;

        try
        {
            EnsureConnection();
            var routingKey = AgentObservationMapper.BuildEventRoutingKey(payload.DomainId, payload.Key);
            var json = AgentObservationMapper.SerializeEventPayload(payload);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(AgentObservationMapper.ExchangeName, routingKey, props, body);
            _logger.LogDebug("Published agent observation to {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish agent observation domain={Domain} key={Key}",
                payload.DomainName,
                payload.Key);
        }

        return Task.CompletedTask;
    }

    private void EnsureConnection()
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            return;

        lock (_channelLock)
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
                return;

            var factory = new ConnectionFactory
            {
                HostName = _rabbit.Host,
                Port = _rabbit.Port,
                UserName = _rabbit.Username,
                Password = _rabbit.Password,
                VirtualHost = _rabbit.VirtualHost
            };

            _connection?.Dispose();
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(
                AgentObservationMapper.ExchangeName,
                ExchangeType.Topic,
                durable: true);
        }
    }

    public void Dispose()
    {
        try
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
        catch
        {
            // ignore dispose failures
        }

        GC.SuppressFinalize(this);
    }
}
