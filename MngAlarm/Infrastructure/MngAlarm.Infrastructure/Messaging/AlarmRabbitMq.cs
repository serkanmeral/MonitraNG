using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using RabbitMQ.Client;

namespace MngAlarm.Infrastructure.Messaging;

public sealed class AlarmRabbitMqConnectionManager : IAsyncDisposable
{
    private readonly MngAlarmSettings _settings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public AlarmRabbitMqConnectionManager(IOptions<MngAlarmSettings> settings) =>
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

public sealed class AlarmTopologyBootstrapper
{
    private readonly AlarmRabbitMqConnectionManager _connectionManager;
    private int _initialized;

    public AlarmTopologyBootstrapper(AlarmRabbitMqConnectionManager connectionManager) =>
        _connectionManager = connectionManager;

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            return;

        var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            AlarmMessagingConstants.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            AlarmMessagingConstants.ObservationExchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            AlarmMessagingConstants.ObservationQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            AlarmMessagingConstants.ObservationQueue,
            AlarmMessagingConstants.ObservationExchange,
            routingKey: AlarmMessagingConstants.ObservationRoutingPattern,
            cancellationToken: cancellationToken);
    }

    public async Task EnsureReactorBridgeAsync(ReactorBridgeSettings settings, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
            return;

        await EnsureAsync(cancellationToken);

        var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            settings.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            AlarmMessagingConstants.ReactorMetricsBridgeQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            AlarmMessagingConstants.ReactorMetricsBridgeQueue,
            settings.Exchange,
            routingKey: settings.RoutingPattern,
            cancellationToken: cancellationToken);
    }
}

public sealed class AlarmEventPublisher : IAlarmEventPublisher
{
    private readonly AlarmRabbitMqConnectionManager _connectionManager;
    private readonly AlarmTopologyBootstrapper _topology;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public AlarmEventPublisher(
        AlarmRabbitMqConnectionManager connectionManager,
        AlarmTopologyBootstrapper topology)
    {
        _connectionManager = connectionManager;
        _topology = topology;
    }

    public async Task PublishAsync(AlarmEventMessage message, string lifecycle, CancellationToken cancellationToken = default)
    {
        await _topology.EnsureAsync(cancellationToken);
        await EnsureChannelAsync(cancellationToken);

        var routingKey = $"{message.DomainId}.{lifecycle}.{message.Severity}";
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = $"{message.AlarmId}:{lifecycle}",
            CorrelationId = message.CorrelationId
        };

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _channel!.BasicPublishAsync(
                AlarmMessagingConstants.Exchange,
                routingKey,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return;

            var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
            _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed class ObservationIngressPublisher : IObservationIngressPublisher
{
    private readonly AlarmRabbitMqConnectionManager _connectionManager;
    private readonly AlarmTopologyBootstrapper _topology;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public ObservationIngressPublisher(
        AlarmRabbitMqConnectionManager connectionManager,
        AlarmTopologyBootstrapper topology)
    {
        _connectionManager = connectionManager;
        _topology = topology;
    }

    public async Task PublishAsync(ObservationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        await _topology.EnsureAsync(cancellationToken);
        await EnsureChannelAsync(cancellationToken);

        var routingKey = MetricObservationMapper.BuildRoutingKey(envelope);
        var payload = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N")
        };

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _channel!.BasicPublishAsync(
                AlarmMessagingConstants.ObservationExchange,
                routingKey,
                mandatory: false,
                basicProperties: props,
                body: payload,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return;

            var connection = await _connectionManager.GetConnectionAsync(cancellationToken);
            _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
