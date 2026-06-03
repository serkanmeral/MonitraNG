using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MngWorkflow.Infrastructure.Messaging;

public sealed class WorkflowEventTopologyBootstrapper
{
    private readonly WorkflowRabbitMqConnectionManager _connectionManager;
    private readonly EventTriggerSettings _settings;
    private int _initialized;

    public WorkflowEventTopologyBootstrapper(
        WorkflowRabbitMqConnectionManager connectionManager,
        IOptions<MngWorkflowSettings> settings)
    {
        _connectionManager = connectionManager;
        _settings = settings.Value.Engine.EventTrigger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return;

        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            return;

        var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            WorkflowEventExchanges.InboundQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            WorkflowEventExchanges.OcEvents,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            WorkflowEventExchanges.InboundQueue,
            WorkflowEventExchanges.OcEvents,
            routingKey: _settings.OcEventsRoutingPattern,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            WorkflowEventExchanges.Alarms,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            WorkflowEventExchanges.InboundQueue,
            WorkflowEventExchanges.Alarms,
            routingKey: _settings.AlarmsRoutingPattern,
            cancellationToken: cancellationToken);
    }
}

public sealed class WorkflowEventTriggerConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkflowRabbitMqConnectionManager _connectionManager;
    private readonly WorkflowEventTopologyBootstrapper _topology;
    private readonly EventTriggerSettings _settings;
    private readonly ILogger<WorkflowEventTriggerConsumer> _logger;

    public WorkflowEventTriggerConsumer(
        IServiceScopeFactory scopeFactory,
        WorkflowRabbitMqConnectionManager connectionManager,
        WorkflowEventTopologyBootstrapper topology,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowEventTriggerConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _topology = topology;
        _settings = settings.Value.Engine.EventTrigger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Workflow event trigger consumer disabled");
            return;
        }

        await _topology.EnsureAsync(stoppingToken);

        var connection = await _connectionManager.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.BasicQosAsync(0, prefetchCount: 8, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IWorkflowEventTriggerProcessor>();
                await processor.ProcessAsync(
                    args.Exchange,
                    args.RoutingKey,
                    args.Body,
                    stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event trigger processing failed routingKey={RoutingKey}", args.RoutingKey);
                await channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: WorkflowEventExchanges.InboundQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Workflow event trigger consumer started queue={Queue} ocPattern={OcPattern} alarmPattern={AlarmPattern}",
            WorkflowEventExchanges.InboundQueue,
            _settings.OcEventsRoutingPattern,
            _settings.AlarmsRoutingPattern);

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
