using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Domain;
using MngReactor.Application.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MngReactor.Infrastructure.Services;

/// <summary>
/// RabbitMQ system.mngkeeper.domain.created event'ini dinler; yeni tenant için varsayılan kayıtları oluşturur.
/// </summary>
public class DomainCreatedEventConsumer : BackgroundService
{
    private const string ExchangeName = "mng.topics";
    private const string RoutingKey = "system.mngkeeper.domain.created";
    private const string QueueName = "mngreactor.domain.created";

    private readonly ILogger<DomainCreatedEventConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitmqSettings _rabbitSettings;
    private IConnection? _connection;
    private IModel? _channel;
    private EventingBasicConsumer? _consumer;

    public DomainCreatedEventConsumer(
        ILogger<DomainCreatedEventConsumer> logger,
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
            _logger.LogWarning("RabbitMQ Host boş - DomainCreatedEventConsumer devre dışı");
            return;
        }

        _logger.LogInformation("DomainCreatedEventConsumer başlatılıyor...");
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

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
                _logger.LogError(ex, "DomainCreatedEventConsumer hata, 10 sn sonra yeniden denenecek");
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
                await HandleDomainCreatedAsync(messageJson);
                _channel!.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Domain created event işlenirken hata");
                try { _channel!.BasicNack(ea.DeliveryTag, false, true); } catch { /* ignore */ }
            }
        };

        _channel.BasicConsume(QueueName, autoAck: false, consumer: _consumer);
        _logger.LogInformation("DomainCreatedEventConsumer dinliyor. Queue: {Queue}, RoutingKey: {Key}", QueueName, RoutingKey);

        await Task.CompletedTask;
    }

    private async Task HandleDomainCreatedAsync(string messageJson)
    {
        string? domainName = null;
        using (var doc = JsonDocument.Parse(messageJson))
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("payload", out var payload) && payload.TryGetProperty("domainName", out var dn))
                domainName = dn.GetString();
            if (string.IsNullOrEmpty(domainName) && root.TryGetProperty("payload", out var p2) && p2.TryGetProperty("databaseName", out var dbn))
            {
                var dbName = dbn.GetString();
                if (dbName?.StartsWith("mng_") == true)
                    domainName = dbName["mng_".Length..];
            }
        }

        if (string.IsNullOrEmpty(domainName))
        {
            _logger.LogWarning("Domain created event'te domainName/databaseName bulunamadı");
            return;
        }

        _logger.LogInformation("Domain created event alındı: {DomainName}, varsayılan kayıtlar oluşturuluyor", domainName);

        using var scope = _scopeFactory.CreateScope();
        var defaultsService = scope.ServiceProvider.GetRequiredService<IDomainDefaultsService>();
        var ok = await defaultsService.CreateDefaultsAsync(domainName);
        if (ok)
            _logger.LogInformation("Domain {DomainName} için varsayılanlar oluşturuldu", domainName);
        else
            _logger.LogWarning("Domain {DomainName} için varsayılan oluşturma başarısız veya zaten mevcut", domainName);
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
        catch (Exception ex) { _logger.LogError(ex, "DomainCreatedEventConsumer kapatılırken hata"); }
        await base.StopAsync(cancellationToken);
    }
}
