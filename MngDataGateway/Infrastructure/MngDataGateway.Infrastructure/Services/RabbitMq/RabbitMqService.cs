using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MngDataGateway.Infrastructure.Services.RabbitMq
{
    /// <summary>
    /// RabbitMQ Service implementation with domain-based exchange strategy
    /// </summary>
    public class RabbitMqService : IRabbitMqService, IDisposable
    {
        private readonly ILogger<RabbitMqService> _logger;
        private readonly Rabbitmq _rabbitMqSettings;
        private IConnection? _connection;
        private IChannel? _channel;
        private readonly HashSet<string> _declaredExchanges = new();
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed = false;

        public RabbitMqService(
            ILogger<RabbitMqService> logger,
            IOptions<MngDataGatewaySettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rabbitMqSettings = settings?.Value?.RabbitMQ ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsConnected => _connection?.IsOpen ?? false;

        public async Task ConnectAsync()
        {
            if (IsConnected)
            {
                _logger.LogDebug("RabbitMQ already connected");
                return;
            }

            await _connectionLock.WaitAsync();
            try
            {
                if (IsConnected) return;

                _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", 
                    _rabbitMqSettings.Host, 
                    _rabbitMqSettings.Port);

                var factory = new ConnectionFactory
                {
                    HostName = _rabbitMqSettings.Host,
                    Port = _rabbitMqSettings.Port,
                    UserName = _rabbitMqSettings.Username,
                    Password = _rabbitMqSettings.Password,
                    VirtualHost = _rabbitMqSettings.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat = TimeSpan.FromSeconds(60)
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

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

        public async Task EnsureExchangeAsync(string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                throw new ArgumentException("Domain name cannot be empty", nameof(domainName));

            var exchangeName = GetExchangeName(domainName);

            // Check if already declared (cache)
            if (_declaredExchanges.Contains(exchangeName))
            {
                _logger.LogDebug("Exchange {ExchangeName} already declared", exchangeName);
                return;
            }

            await EnsureConnectedAsync();

            try
            {
                _logger.LogInformation("Declaring exchange: {ExchangeName}", exchangeName);

                await _channel!.ExchangeDeclareAsync(
                    exchange: exchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null
                );

                _declaredExchanges.Add(exchangeName);
                _logger.LogInformation("Exchange {ExchangeName} declared successfully", exchangeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare exchange {ExchangeName}", exchangeName);
                throw;
            }
        }

        public async Task PublishDataEventAsync(string domainName, string routingKey, object eventPayload, int retryCount = 3)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                throw new ArgumentException("Domain name cannot be empty", nameof(domainName));

            if (string.IsNullOrWhiteSpace(routingKey))
                throw new ArgumentException("Routing key cannot be empty", nameof(routingKey));

            if (eventPayload == null)
                throw new ArgumentNullException(nameof(eventPayload));

            var exchangeName = GetExchangeName(domainName);
            await EnsureExchangeAsync(domainName);

            // Serialize payload
            var jsonPayload = JsonSerializer.Serialize(eventPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var body = Encoding.UTF8.GetBytes(jsonPayload);

            // Retry logic
            for (int attempt = 1; attempt <= retryCount; attempt++)
            {
                try
                {
                    await PublishInternalAsync(exchangeName, routingKey, body, eventPayload);
                    
                    _logger.LogInformation(
                        "Event published successfully to exchange {Exchange} with routing key {RoutingKey}",
                        exchangeName, routingKey);
                    
                    return; // Success
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Failed to publish event (attempt {Attempt}/{RetryCount})", 
                        attempt, retryCount);

                    if (attempt == retryCount)
                    {
                        // All retries failed - log to error collection
                        _logger.LogError(ex,
                            "Failed to publish event after {RetryCount} attempts. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
                            retryCount, exchangeName, routingKey);

                        // Note: Error logging to MongoDB will be handled by NotificationService
                        throw;
                    }

                    // Exponential backoff
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100);
                    await Task.Delay(delay);

                    // Try to reconnect if connection lost
                    if (!IsConnected)
                    {
                        try
                        {
                            await ConnectAsync();
                            await EnsureExchangeAsync(domainName);
                        }
                        catch (Exception reconnectEx)
                        {
                            _logger.LogError(reconnectEx, "Failed to reconnect to RabbitMQ");
                        }
                    }
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (_channel != null)
            {
                if (_channel.IsOpen)
                {
                    await _channel.CloseAsync();
                }
                _channel.Dispose();
                _channel = null;
            }

            if (_connection != null)
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }
                _connection.Dispose();
                _connection = null;
            }

            _declaredExchanges.Clear();
            _logger.LogInformation("RabbitMQ disconnected");
        }

        private async Task PublishInternalAsync(string exchangeName, string routingKey, byte[] body, object eventPayload)
        {
            await EnsureConnectedAsync();

            // Get event ID for correlation
            var eventId = GetEventId(eventPayload);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = eventId,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>
                {
                    { "x-event-version", "1.0" },
                    { "x-source-service", "MngDataGateway" }
                }
            };

            await _channel!.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body
            );
        }

        private async Task EnsureConnectedAsync()
        {
            if (!IsConnected)
            {
                await ConnectAsync();
            }
        }

        private static string GetExchangeName(string domainName)
        {
            return $"monitra.data.events.{domainName.ToLowerInvariant()}";
        }

        private const string UnifiedExchangeName = "mngdatagateway.events";
        private const string MonitoringSyncExchangeName = "monitra.monitoring.sync";

        public async Task PublishMonitoringSyncEventAsync(string domainName, string datasetName, string operation, object eventPayload)
        {
            if (string.IsNullOrWhiteSpace(domainName) || string.IsNullOrWhiteSpace(datasetName) || string.IsNullOrWhiteSpace(operation))
                return;
            if (eventPayload == null)
                throw new ArgumentNullException(nameof(eventPayload));

            await EnsureMonitoringSyncExchangeAsync();
            var routingKey = $"{domainName.ToLowerInvariant()}.{datasetName}.{operation}";

            var jsonPayload = JsonSerializer.Serialize(eventPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
            var body = Encoding.UTF8.GetBytes(jsonPayload);

            try
            {
                await PublishInternalAsync(MonitoringSyncExchangeName, routingKey, body, eventPayload);
                _logger.LogDebug("Monitoring sync event published: {Exchange} {RoutingKey}", MonitoringSyncExchangeName, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish monitoring sync event: {RoutingKey}", routingKey);
            }
        }

        public async Task EnsureMonitoringSyncExchangeAsync()
        {
            if (_declaredExchanges.Contains(MonitoringSyncExchangeName))
                return;
            await EnsureConnectedAsync();
            try
            {
                await _channel!.ExchangeDeclareAsync(
                    exchange: MonitoringSyncExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null);
                _declaredExchanges.Add(MonitoringSyncExchangeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare monitoring sync exchange");
                throw;
            }
        }

        public async Task EnsureUnifiedExchangeAsync()
        {
            // Check if already declared (cache)
            if (_declaredExchanges.Contains(UnifiedExchangeName))
            {
                _logger.LogDebug("Unified exchange {ExchangeName} already declared", UnifiedExchangeName);
                return;
            }

            await EnsureConnectedAsync();

            try
            {
                _logger.LogInformation("Declaring unified exchange: {ExchangeName}", UnifiedExchangeName);

                await _channel!.ExchangeDeclareAsync(
                    exchange: UnifiedExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null
                );

                _declaredExchanges.Add(UnifiedExchangeName);
                _logger.LogInformation("Unified exchange {ExchangeName} declared successfully", UnifiedExchangeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare unified exchange {ExchangeName}", UnifiedExchangeName);
                throw;
            }
        }

        public async Task PublishToUnifiedExchangeAsync(string domainId, string routingKey, object eventPayload)
        {
            if (string.IsNullOrWhiteSpace(domainId))
                throw new ArgumentException("Domain ID cannot be empty", nameof(domainId));

            if (string.IsNullOrWhiteSpace(routingKey))
                throw new ArgumentException("Routing key cannot be empty", nameof(routingKey));

            if (eventPayload == null)
                throw new ArgumentNullException(nameof(eventPayload));

            await EnsureUnifiedExchangeAsync();

            // Serialize payload
            var jsonPayload = JsonSerializer.Serialize(eventPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var body = Encoding.UTF8.GetBytes(jsonPayload);

            try
            {
                await PublishInternalAsync(UnifiedExchangeName, routingKey, body, eventPayload);
                
                _logger.LogInformation(
                    "Event published successfully to unified exchange {Exchange} with routing key {RoutingKey}",
                    UnifiedExchangeName, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish event to unified exchange {Exchange}, RoutingKey: {RoutingKey}",
                    UnifiedExchangeName, routingKey);
                throw;
            }
        }

        private static string GetEventId(object eventPayload)
        {
            // Try to extract eventId from payload using reflection
            var eventIdProperty = eventPayload.GetType().GetProperty("EventId");
            if (eventIdProperty != null)
            {
                var value = eventIdProperty.GetValue(eventPayload);
                if (value != null)
                    return value.ToString()!;
            }

            // Fallback to new GUID
            return Guid.NewGuid().ToString();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                DisconnectAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RabbitMQ disposal");
            }
            finally
            {
                _connectionLock?.Dispose();
            }
            
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

