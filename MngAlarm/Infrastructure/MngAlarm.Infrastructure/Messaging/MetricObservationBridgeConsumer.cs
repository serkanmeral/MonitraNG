using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MngAlarm.Infrastructure.Messaging;

/// <summary>
/// Bridges MngReactor metric inserts (mng.topics) into monitra.observations for MngAlarm.
/// </summary>
public sealed class MetricObservationBridgeConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlarmRabbitMqConnectionManager _connectionManager;
    private readonly AlarmTopologyBootstrapper _topology;
    private readonly ReactorBridgeSettings _settings;
    private readonly ILogger<MetricObservationBridgeConsumer> _logger;

    public MetricObservationBridgeConsumer(
        IServiceScopeFactory scopeFactory,
        AlarmRabbitMqConnectionManager connectionManager,
        AlarmTopologyBootstrapper topology,
        IOptions<MngAlarmSettings> settings,
        ILogger<MetricObservationBridgeConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _topology = topology;
        _settings = settings.Value.Engine.ReactorBridge;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Reactor metric observation bridge disabled");
            return;
        }

        await _topology.EnsureReactorBridgeAsync(_settings, stoppingToken);

        var connection = await _connectionManager.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, _settings.Prefetch, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var envelope = MetricObservationMapper.TryMap(args.Body.Span);
                if (envelope != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IObservationIngressPublisher>();
                    await publisher.PublishAsync(envelope, stoppingToken);
                }
                else
                {
                    _logger.LogDebug(
                        "Reactor metric bridge skipped unmapped message routingKey={RoutingKey}",
                        args.RoutingKey);
                }

                await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reactor metric bridge failed routingKey={RoutingKey}", args.RoutingKey);
                await channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            AlarmMessagingConstants.ReactorMetricsBridgeQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Reactor metric bridge started queue={Queue} exchange={Exchange} pattern={Pattern}",
            AlarmMessagingConstants.ReactorMetricsBridgeQueue,
            _settings.Exchange,
            _settings.RoutingPattern);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
