using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MngAlarm.Infrastructure.Messaging;

public sealed class ObservationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlarmRabbitMqConnectionManager _connectionManager;
    private readonly AlarmTopologyBootstrapper _topology;
    private readonly EngineSettings _engine;
    private readonly ILogger<ObservationConsumer> _logger;

    public ObservationConsumer(
        IServiceScopeFactory scopeFactory,
        AlarmRabbitMqConnectionManager connectionManager,
        AlarmTopologyBootstrapper topology,
        IOptions<MngAlarmSettings> settings,
        ILogger<ObservationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _topology = topology;
        _engine = settings.Value.Engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_engine.ConsumeObservations)
        {
            _logger.LogInformation("Observation consumer disabled");
            return;
        }

        await _topology.EnsureAsync(stoppingToken);

        var connection = await _connectionManager.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, _engine.ObservationPrefetch, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var envelope = ObservationIngressParser.TryParse(args.Body.Span);

                if (envelope != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<IObservationProcessor>();
                    await processor.ProcessAsync(envelope, stoppingToken);
                }
                else
                {
                    _logger.LogDebug(
                        "Observation skipped unmapped message routingKey={RoutingKey}",
                        args.RoutingKey);
                }

                await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Observation processing failed");
                await channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            AlarmMessagingConstants.ObservationQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Observation consumer started queue={Queue}", AlarmMessagingConstants.ObservationQueue);

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
