using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Infrastructure.Serialization;
using RabbitMQ.Client;

namespace MngWorkflow.Infrastructure.Messaging;

public sealed class WorkflowRabbitMqConnectionManager : IAsyncDisposable
{
    private readonly MngWorkflowSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public WorkflowRabbitMqConnectionManager(IOptions<MngWorkflowSettings> settings) =>
        _settings = settings.Value;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMq.Host,
                Port = _settings.RabbitMq.Port,
                UserName = _settings.RabbitMq.Username,
                Password = _settings.RabbitMq.Password,
                VirtualHost = _settings.RabbitMq.VirtualHost,
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.CloseAsync();
        _gate.Dispose();
    }
}

public sealed class WorkflowTopologyBootstrapper
{
    private readonly WorkflowRabbitMqConnectionManager _connectionManager;
    private int _initialized;

    public WorkflowTopologyBootstrapper(WorkflowRabbitMqConnectionManager connectionManager) =>
        _connectionManager = connectionManager;

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            return;

        var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            WorkflowMessagingConstants.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            WorkflowMessagingConstants.ExecutionQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            WorkflowMessagingConstants.ExecutionQueue,
            WorkflowMessagingConstants.Exchange,
            routingKey: WorkflowMessagingConstants.RetryRoutingKey,
            cancellationToken: cancellationToken);

        foreach (var bucket in WorkflowMessagingConstants.RetryBuckets)
        {
            var args = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = bucket.TtlMilliseconds,
                ["x-dead-letter-exchange"] = WorkflowMessagingConstants.Exchange,
                ["x-dead-letter-routing-key"] = WorkflowMessagingConstants.RetryRoutingKey
            };

            await channel.QueueDeclareAsync(
                bucket.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                cancellationToken: cancellationToken);
        }

        foreach (var bucket in WorkflowMessagingConstants.DelayBuckets)
        {
            var args = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = bucket.TtlMilliseconds,
                ["x-dead-letter-exchange"] = WorkflowMessagingConstants.Exchange,
                ["x-dead-letter-routing-key"] = WorkflowMessagingConstants.ResumeRoutingKey
            };

            await channel.QueueDeclareAsync(
                bucket.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(
            WorkflowMessagingConstants.ResumeQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            WorkflowMessagingConstants.ResumeQueue,
            WorkflowMessagingConstants.Exchange,
            routingKey: WorkflowMessagingConstants.ResumeRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            WorkflowMessagingConstants.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            WorkflowMessagingConstants.ExecutionQueue,
            WorkflowMessagingConstants.Exchange,
            routingKey: "*." + WorkflowMessagingConstants.ExecutionRoutingSuffix,
            cancellationToken: cancellationToken);
    }
}

public sealed class WorkflowQueuePublisher : IWorkflowQueuePublisher
{
    private readonly WorkflowRabbitMqConnectionManager _connectionManager;
    private readonly WorkflowTopologyBootstrapper _topology;
    private readonly SemaphoreSlim _publishGate = new(1, 1);
    private IChannel? _channel;

    public WorkflowQueuePublisher(
        WorkflowRabbitMqConnectionManager connectionManager,
        WorkflowTopologyBootstrapper topology)
    {
        _connectionManager = connectionManager;
        _topology = topology;
    }

    public async Task PublishRetryAsync(WorkflowExecutionMessage message, int failedAttempt, CancellationToken cancellationToken = default)
    {
        await _topology.EnsureAsync(cancellationToken);
        await EnsureChannelAsync(cancellationToken);

        var queueName = WorkflowRetryBucketResolver.ResolveQueueName(failedAttempt);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WorkflowJsonDefaults.Message);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = $"{message.InstanceId}:{message.NodeId}:{message.Attempt}",
            CorrelationId = message.CorrelationId
        };

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    public async Task PublishDeadLetterAsync(WorkflowDeadLetterMessage message, CancellationToken cancellationToken = default)
    {
        await _topology.EnsureAsync(cancellationToken);
        await EnsureChannelAsync(cancellationToken);

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WorkflowJsonDefaults.Message);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = $"{message.Execution.InstanceId}:{message.Execution.NodeId}:{message.Execution.Attempt}",
            CorrelationId = message.Execution.CorrelationId
        };

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: WorkflowMessagingConstants.DeadLetterQueue,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    public async Task PublishDelayResumeAsync(WorkflowResumeMessage message, int delaySeconds, CancellationToken cancellationToken = default)
    {
        await _topology.EnsureAsync(cancellationToken);
        await EnsureChannelAsync(cancellationToken);

        var queueName = WorkflowDelayBucketResolver.ResolveQueueName(delaySeconds);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WorkflowJsonDefaults.Message);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = $"{message.InstanceId}:{message.NodeId}:delay",
            CorrelationId = message.CorrelationId
        };

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            await _channel!.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    public async Task PublishExecutionAsync(WorkflowExecutionMessage message, CancellationToken cancellationToken = default)
    {
        await _topology.EnsureAsync(cancellationToken);
        await EnsureChannelAsync(cancellationToken);

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WorkflowJsonDefaults.Message);
        var routingKey = $"{message.DomainId}.{WorkflowMessagingConstants.ExecutionRoutingSuffix}";

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = $"{message.InstanceId}:{message.NodeId}:{message.Attempt}",
            CorrelationId = message.CorrelationId
        };

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            await _channel!.BasicPublishAsync(
                WorkflowMessagingConstants.Exchange,
                routingKey,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return;

        await _publishGate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return;

            var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
            _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _publishGate.Release();
        }
    }
}
