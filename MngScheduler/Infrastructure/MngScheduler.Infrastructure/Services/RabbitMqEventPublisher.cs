using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;
using RabbitMQ.Client;

namespace MngScheduler.Infrastructure.Services;

/// <summary>
/// RabbitMQ event publisher for job execution events
/// </summary>
public class RabbitMqEventPublisher : IRabbitMqEventPublisher, IDisposable
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly MngSchedulerSettings _settings;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private const string ExchangeName = "mng_scheduler_events";
    private const string RoutingKeyPrefix = "job.execution";

    public RabbitMqEventPublisher(
        ILogger<RabbitMqEventPublisher> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task PublishJobExecutionCompletedAsync(JobExecution execution, ScheduledJob job)
    {
        try
        {
            await EnsureConnectedAsync();

            // Create event payload
            var eventPayload = new
            {
                EventType = "job.execution.completed",
                JobId = execution.JobId,
                ExecutionId = execution.ExecutionId,
                JobType = job.JobType.ToString(),
                Status = execution.Status,
                ExecutedAt = execution.ExecutedAt,
                ResponseTimeMs = execution.ResponseTimeMs,
                ResponseCode = execution.ResponseCode,
                ErrorMessage = execution.ErrorMessage,
                DomainId = execution.DomainId,
                TotalExecutionCount = job.TotalExecutionCount,
                SuccessfulExecutionCount = job.SuccessfulExecutionCount,
                FailedExecutionCount = job.FailedExecutionCount,
                MaxExecutionCount = job.MaxExecutionCount,
                IsActive = job.IsActive,
                Timestamp = DateTime.UtcNow
            };

            var message = JsonSerializer.Serialize(eventPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var routingKey = $"{RoutingKeyPrefix}.{execution.Status}";
            if (job.JobType == JobType.User && !string.IsNullOrEmpty(execution.DomainId))
            {
                routingKey = $"{RoutingKeyPrefix}.{job.JobType.ToString().ToLowerInvariant()}.{execution.Status}";
            }

            await PublishAsync(ExchangeName, routingKey, message);

            _logger.LogDebug("Published job execution event: {JobId}, {ExecutionId}, Status: {Status}",
                execution.JobId, execution.ExecutionId, execution.Status);
        }
        catch (Exception ex)
        {
            // Don't throw - event publishing failure shouldn't break job execution
            _logger.LogError(ex, "Failed to publish job execution event: {JobId}, {ExecutionId}",
                execution.JobId, execution.ExecutionId);
        }
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection?.IsOpen == true && _channel?.IsOpen == true)
        {
            return;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_connection?.IsOpen == true && _channel?.IsOpen == true)
            {
                return;
            }

            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}",
                _settings.RabbitMQ.Host, _settings.RabbitMQ.Port);

            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMQ.Host,
                Port = _settings.RabbitMQ.Port,
                UserName = _settings.RabbitMQ.Username,
                Password = _settings.RabbitMQ.Password,
                VirtualHost = _settings.RabbitMQ.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Declare exchange
            await _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            _logger.LogInformation("RabbitMQ connected successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task PublishAsync(string exchange, string routingKey, string message)
    {
        await EnsureConnectedAsync();

        try
        {
            var body = Encoding.UTF8.GetBytes(message);
            var properties = new BasicProperties
            {
                Persistent = true,
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            await _channel!.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body);

            _logger.LogDebug("Published message to RabbitMQ. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
                exchange, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to RabbitMQ. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
                exchange, routingKey);
            throw;
        }
    }

    public void Dispose()
    {
        if (_channel != null)
        {
            if (_channel.IsOpen)
            {
                _channel.CloseAsync().GetAwaiter().GetResult();
            }
            _channel.Dispose();
        }

        if (_connection != null)
        {
            if (_connection.IsOpen)
            {
                _connection.CloseAsync().GetAwaiter().GetResult();
            }
            _connection.Dispose();
        }

        _connectionLock?.Dispose();
    }
}
