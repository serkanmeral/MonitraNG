using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Configuration;
using RabbitMQ.Client;

namespace MngReactor.Infrastructure.Services;

public class MetricPublisher : IMetricPublisher, IDisposable
{
    private const string Exchange = "mng.topics";
    private const string RoutingKeyPrefix = "monitoring.metric.inserted.";

    private readonly ILogger<MetricPublisher> _logger;
    private readonly RabbitmqSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;

    public MetricPublisher(
        ILogger<MetricPublisher> logger,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _settings = options.Value.RabbitMQ;
    }

    private void EnsureConnection()
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

    public Task PublishAsync(object metricDocument, string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureConnection();
            var routingKey = $"{RoutingKeyPrefix}{domain}";
            var json = JsonSerializer.Serialize(metricDocument);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;

            _channel.BasicPublish(Exchange, routingKey, props, body);
            _logger.LogDebug("Published metric to {RoutingKey}", routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish metric to RabbitMQ for domain {Domain}", domain);
            // Fire-and-forget: metrik publish hatası ingest'i başarısız yapmaz
        }

        return Task.CompletedTask;
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
