using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Configuration;
using RabbitMQ.Client;

namespace MngReactor.Infrastructure.Services;

/// <summary>
/// Ingest başarılı olduktan sonra UI için tek, domain bazlı throttle'lu "data.updated" event yayınlar.
/// </summary>
public class IngestNotifyPublisher : IIngestNotifyPublisher, IDisposable
{
    private const string Exchange = "mng.topics";
    private const string RoutingKeyPrefix = "monitoring.data.updated.";

    private readonly ILogger<IngestNotifyPublisher> _logger;
    private readonly RabbitmqSettings _rabbitSettings;
    private readonly MonitoringSettings _monitoringSettings;
    private readonly ConcurrentDictionary<string, DateTime> _lastPublishUtcByDomain = new(StringComparer.OrdinalIgnoreCase);
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _channelLock = new();

    public IngestNotifyPublisher(
        ILogger<IngestNotifyPublisher> logger,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _rabbitSettings = options.Value.RabbitMQ;
        _monitoringSettings = options.Value.Monitoring;
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
            _channel.ExchangeDeclare(Exchange, ExchangeType.Topic, durable: true);
        }
    }

    public Task TryPublishDataUpdatedAsync(string domain, DateTime lastIngestAtUtc, IReadOnlyList<string> engineIds, CancellationToken cancellationToken = default)
    {
        if (!_monitoringSettings.IngestNotifyEnabled)
        {
            _logger.LogDebug("IngestNotify disabled, skipping data.updated publish for domain {Domain}", domain);
            return Task.CompletedTask;
        }

        var throttleSeconds = _monitoringSettings.IngestNotifyThrottleSeconds;
        if (throttleSeconds <= 0)
            throttleSeconds = 5;

        var now = DateTime.UtcNow;
        var last = _lastPublishUtcByDomain.GetOrAdd(domain, DateTime.MinValue);
        if ((now - last).TotalSeconds < throttleSeconds)
        {
            _logger.LogDebug("Throttle: skipping data.updated for domain {Domain} (last publish {Seconds:F0}s ago)", domain, (now - last).TotalSeconds);
            return Task.CompletedTask;
        }

        try
        {
            EnsureConnection();
            var routingKey = $"{RoutingKeyPrefix}{domain}";

            var payload = new
            {
                domain,
                lastIngestAtUtc = lastIngestAtUtc.ToString("O"),
                engineIds = engineIds ?? Array.Empty<string>()
            };
            var json = JsonSerializer.Serialize(payload);
            var body = Encoding.UTF8.GetBytes(json);

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;

            _channel.BasicPublish(Exchange, routingKey, props, body);
            _lastPublishUtcByDomain[domain] = now;
            _logger.LogDebug("Published data.updated to {RoutingKey} (engineIds: {Count})", routingKey, engineIds?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish data.updated to RabbitMQ for domain {Domain}", domain);
        }

        return Task.CompletedTask;
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
