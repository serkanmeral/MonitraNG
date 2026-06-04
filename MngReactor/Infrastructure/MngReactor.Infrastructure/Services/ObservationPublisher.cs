using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Observations;
using MngReactor.Application.Configuration;
using MngReactor.Application.Observations;
using RabbitMQ.Client;

namespace MngReactor.Infrastructure.Services;

public class ObservationPublisher : IObservationPublisher, IDisposable
{
    private readonly ILogger<ObservationPublisher> _logger;
    private readonly RabbitmqSettings _rabbitSettings;
    private readonly ObservationPublishSettings _publishSettings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _channelLock = new();

    public ObservationPublisher(
        ILogger<ObservationPublisher> logger,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _rabbitSettings = options.Value.RabbitMQ;
        _publishSettings = options.Value.ObservationPublish;
    }

    public Task PublishAsync(
        string domainId,
        string domainName,
        string collectibleCode,
        double value,
        IReadOnlyDictionary<string, string?>? dimensions = null,
        DateTime? timestamp = null,
        CancellationToken cancellationToken = default)
    {
        if (!_publishSettings.Enabled)
        {
            _logger.LogDebug(
                "ObservationPublish disabled, skipping {Collectible} for domain {Domain}",
                collectibleCode,
                domainName);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(domainName) || string.IsNullOrWhiteSpace(collectibleCode))
            return Task.CompletedTask;

        var resolvedDomainId = string.IsNullOrWhiteSpace(domainId) ? domainName.Trim() : domainId.Trim();

        try
        {
            EnsureConnection();
            var routingKey = ObservationPublishMessage.BuildRoutingKey(resolvedDomainId, collectibleCode);
            var json = ObservationPublishMessage.SerializeFlatPayload(
                resolvedDomainId,
                domainName,
                collectibleCode,
                value,
                dimensions,
                timestamp);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(ObservationPublishMessage.ExchangeName, routingKey, props, body);
            _logger.LogDebug("Published observation to {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish observation for domain {Domain} collectible {Collectible}",
                domainName,
                collectibleCode);
        }

        return Task.CompletedTask;
    }

    public Task PublishSecEventAsync(
        SecEventObservationPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!_publishSettings.Enabled)
        {
            _logger.LogDebug(
                "ObservationPublish disabled, skipping sec_event {Key} for domain {Domain}",
                payload.Key,
                payload.DomainName);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(payload.DomainName) || string.IsNullOrWhiteSpace(payload.Key))
            return Task.CompletedTask;

        try
        {
            EnsureConnection();
            var routingKey = ObservationPublishMessage.BuildEventRoutingKey(payload.DomainId, payload.Key);
            var json = ObservationPublishMessage.SerializeEventPayload(payload);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";

            _channel.BasicPublish(ObservationPublishMessage.ExchangeName, routingKey, props, body);
            _logger.LogDebug("Published sec_event observation to {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish sec_event observation for domain {Domain} key {Key}",
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
                HostName = _rabbitSettings.Host,
                Port = _rabbitSettings.Port,
                UserName = _rabbitSettings.Username,
                Password = _rabbitSettings.Password,
                VirtualHost = _rabbitSettings.VirtualHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(
                ObservationPublishMessage.ExchangeName,
                ExchangeType.Topic,
                durable: true);
        }
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
