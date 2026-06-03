using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MngReactor.Infrastructure.Services;

/// <summary>
/// monitra.monitoring.sync exchange'inden mon_engines, mon_agents, mon_assets event'lerini dinler;
/// ilgili engine'ler için MQTT sync tetikler.
/// </summary>
public class MonitoringSyncEventConsumer : BackgroundService
{
    private const string ExchangeName = "monitra.monitoring.sync";
    private const string QueueName = "mngreactor.monitoring.sync";
    private const string RoutingKey = "#";

    private readonly ILogger<MonitoringSyncEventConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitmqSettings _rabbitSettings;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;

    public MonitoringSyncEventConsumer(
        ILogger<MonitoringSyncEventConsumer> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _rabbitSettings = options.Value.RabbitMQ;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_rabbitSettings.Host))
        {
            _logger.LogInformation("RabbitMQ Host boş - MonitoringSyncEventConsumer devre dışı");
            return;
        }

        _logger.LogInformation("MonitoringSyncEventConsumer başlatılıyor...");
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_connection?.IsOpen != true || _channel?.IsOpen != true)
                {
                    await ConnectAndSubscribeAsync(stoppingToken);
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MonitoringSyncEventConsumer hata, 10 sn sonra yeniden denenecek");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken cancellationToken)
    {
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
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);

        try { _channel.QueueUnbind(QueueName, ExchangeName, RoutingKey); } catch { /* ignore */ }
        _channel.QueueBind(QueueName, ExchangeName, RoutingKey);

        _consumer = new EventingBasicConsumer(_channel);
        _consumer.Received += async (_, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);
                await HandleMonitoringSyncEventAsync(messageJson);
                _channel!.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitoring sync event işlenirken hata");
                try { _channel!.BasicNack(ea.DeliveryTag, false, true); } catch { /* ignore */ }
            }
        };

        _channel.BasicConsume(QueueName, autoAck: false, consumer: _consumer);
        _logger.LogInformation("MonitoringSyncEventConsumer dinliyor. Queue: {Queue}, Exchange: {Exchange}", QueueName, ExchangeName);

        await Task.CompletedTask;
    }

    private async Task HandleMonitoringSyncEventAsync(string messageJson)
    {
        string? domain = null;
        string? dataset = null;
        string? engineId = null;
        string? assetId = null;

        using (var doc = JsonDocument.Parse(messageJson))
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("name", out var dn))
                domain = dn.GetString();
            if (root.TryGetProperty("dataset", out var ds) && ds.ValueKind == JsonValueKind.Object && ds.TryGetProperty("name", out var dsn))
                dataset = dsn.GetString();

            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataset == "mon_engines")
                    engineId = ExtractIdFromJson(dataEl, "__dataId");
                else if (dataset == "mon_agents")
                    engineId = ExtractIdFromJson(dataEl, "engineId") ?? ExtractIdFromJson(dataEl, "EngineId");
                else if (dataset == "mon_assets")
                    assetId = ExtractIdFromJson(dataEl, "__dataId");
            }
        }

        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(dataset))
        {
            _logger.LogDebug("Monitoring sync event atlandı: domain veya dataset boş");
            return;
        }

        if (dataset != "mon_engines" && dataset != "mon_agents" && dataset != "mon_assets")
        {
            _logger.LogDebug("Monitoring sync event atlandı: dataset={Dataset} (ilgili değil)", dataset);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var mqttPublisher = scope.ServiceProvider.GetRequiredService<IMqttSyncPublisher>();

        if (dataset == "mon_engines" && !string.IsNullOrEmpty(engineId))
        {
            await mqttPublisher.PublishSyncAsync(domain, engineId);
            _logger.LogInformation("Monitoring sync: mon_engines değişti, engineId={EngineId} için MQTT sync tetiklendi", engineId);
        }
        else if (dataset == "mon_agents" && !string.IsNullOrEmpty(engineId))
        {
            await mqttPublisher.PublishSyncAsync(domain, engineId);
            _logger.LogInformation("Monitoring sync: mon_agents değişti, engineId={EngineId} için MQTT sync tetiklendi", engineId);
        }
        else if (dataset == "mon_assets" && !string.IsNullOrEmpty(assetId))
        {
            var resolver = scope.ServiceProvider.GetRequiredService<IEngineIdsForAssetResolver>();
            var engineIds = await resolver.GetEngineIdsForAssetAsync(domain, assetId, null, default);
            foreach (var eid in engineIds)
            {
                await mqttPublisher.PublishSyncAsync(domain, eid);
                _logger.LogInformation("Monitoring sync: mon_assets değişti, assetId={AssetId} -> engineId={EngineId} için MQTT sync tetiklendi", assetId, eid);
            }
        }
    }

    private static string? ExtractIdFromJson(JsonElement data, string key)
    {
        if (data.ValueKind == JsonValueKind.Undefined || data.ValueKind == JsonValueKind.Null)
            return null;
        if (!data.TryGetProperty(key, out var val))
            return null;
        if (val.ValueKind == JsonValueKind.Object && val.TryGetProperty("__dataId", out var oid))
            return oid.GetString();
        return val.GetString();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
        }
        catch (Exception ex) { _logger.LogError(ex, "MonitoringSyncEventConsumer kapatılırken hata"); }
        await base.StopAsync(cancellationToken);
    }
}
