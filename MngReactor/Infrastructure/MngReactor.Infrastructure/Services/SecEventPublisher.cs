using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Models.SecEvents;
using RabbitMQ.Client;

namespace MngReactor.Infrastructure.Services;

public sealed class SecEventPublisher : ISecEventPublisher, IDisposable
{
    private const string Exchange = "mng.topics";
    private const string RoutingKeyPrefix = "sec_events.created.";

    private readonly ILogger<SecEventPublisher> _logger;
    private readonly RabbitmqSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _channelLock = new();

    public SecEventPublisher(
        ILogger<SecEventPublisher> logger,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _settings = options.Value.RabbitMQ;
    }

    public Task PublishCreatedAsync(
        string domain,
        IReadOnlyList<SecEventCreatedMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0 || string.IsNullOrWhiteSpace(domain))
            return Task.CompletedTask;

        try
        {
            EnsureConnection();
            var routingKey = $"{RoutingKeyPrefix}{domain.Trim()}";

            foreach (var message in messages)
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                var props = _channel!.CreateBasicProperties();
                props.Persistent = true;
                props.ContentType = "application/json";

                _channel.BasicPublish(Exchange, routingKey, props, body);
            }

            _logger.LogDebug("Published {Count} sec_events.created messages to {RoutingKey}", messages.Count, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish sec_events.created for domain {Domain}", domain);
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
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
