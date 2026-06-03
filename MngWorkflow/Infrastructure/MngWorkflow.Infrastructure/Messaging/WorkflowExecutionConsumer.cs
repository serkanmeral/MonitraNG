using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Infrastructure.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MngWorkflow.Infrastructure.Messaging;

public sealed class WorkflowExecutionConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkflowRabbitMqConnectionManager _connectionManager;
    private readonly WorkflowTopologyBootstrapper _topology;
    private readonly EngineSettings _engine;
    private readonly ILogger<WorkflowExecutionConsumer> _logger;

    public WorkflowExecutionConsumer(
        IServiceScopeFactory scopeFactory,
        WorkflowRabbitMqConnectionManager connectionManager,
        WorkflowTopologyBootstrapper topology,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowExecutionConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _topology = topology;
        _engine = settings.Value.Engine;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _topology.EnsureAsync(stoppingToken);

        var connection = await _connectionManager.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.BasicQosAsync(0, _engine.PrefetchCount, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<WorkflowExecutionMessage>(args.Body.ToArray(), WorkflowJsonDefaults.Message);
                if (message == null)
                {
                    await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionEngine>();
                await engine.ProcessMessageAsync(message, stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing workflow message deliveryTag={Tag}", args.DeliveryTag);
                await channel.BasicNackAsync(args.DeliveryTag, false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: WorkflowMessagingConstants.ExecutionQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Workflow execution consumer started queue={Queue} prefetch={Prefetch}",
            WorkflowMessagingConstants.ExecutionQueue,
            _engine.PrefetchCount);

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
