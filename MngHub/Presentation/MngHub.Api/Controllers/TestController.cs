using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MngHub.Application.Services;
using MngHub.Application.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MngHub.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class TestController : ControllerBase
{
    private readonly IConnectionManager _connectionManager;
    private readonly IRabbitMqConsumer _rabbitMqConsumer;
    private readonly ILogger<TestController> _logger;
    private readonly MngHubSettings _settings;

    public TestController(
        IConnectionManager connectionManager,
        IRabbitMqConsumer rabbitMqConsumer,
        ILogger<TestController> logger,
        IOptions<MngHubSettings> settings)
    {
        _connectionManager = connectionManager;
        _rabbitMqConsumer = rabbitMqConsumer;
        _logger = logger;
        _settings = settings.Value;
    }

    /// <summary>
    /// Get all active connections
    /// </summary>
    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections()
    {
        var connections = await _connectionManager.GetAllConnectionsAsync();
        return Ok(connections);
    }

    /// <summary>
    /// Get connections by domain
    /// </summary>
    [HttpGet("connections/domain/{domainName}")]
    public async Task<IActionResult> GetConnectionsByDomain(string domainName)
    {
        var connections = await _connectionManager.GetConnectionsByDomainAsync(domainName);
        return Ok(connections);
    }

    /// <summary>
    /// Check if connection exists
    /// </summary>
    [HttpGet("connections/{connectionId}")]
    public async Task<IActionResult> GetConnection(string connectionId)
    {
        var connection = await _connectionManager.GetConnectionAsync(connectionId);
        if (connection == null)
        {
            return NotFound(new { message = "Connection not found", connectionId });
        }
        return Ok(connection);
    }

    /// <summary>
    /// Test endpoint - returns service status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            service = "MngHub",
            status = "running",
            timestamp = DateTime.UtcNow,
            endpoints = new
            {
                signalR = "/ws/v1",
                signalRLegacy = "/ws",
                health = "/health",
                connections = "/api/v1/test/connections"
            }
        });
    }

    /// <summary>
    /// Publish a test domain created event to RabbitMQ
    /// </summary>
    [HttpPost("publish-test-domain-event")]
    public async Task<IActionResult> PublishTestDomainEvent()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMQ.Host,
                Port = _settings.RabbitMQ.Port,
                UserName = _settings.RabbitMQ.Username,
                Password = _settings.RabbitMQ.Password,
                VirtualHost = _settings.RabbitMQ.VirtualHost
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Ensure exchange exists
            channel.ExchangeDeclare(
                exchange: _settings.RabbitMQ.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // Create test domain created event message
            var testEvent = new
            {
                eventId = Guid.NewGuid().ToString(),
                eventType = "system.mngkeeper.domain.created",
                timestamp = DateTime.UtcNow,
                source = "MngHub-Test",
                version = "1.0",
                payload = new
                {
                    domainId = "test-domain-id-123",
                    domainName = "test-domain-manual",
                    databaseName = "mng_test-domain-manual",
                    realmName = "test-domain-manual",
                    bucketName = "mng-test-domain-manual",
                    status = "Active",
                    adminEmail = "admin@test.com",
                    createdAt = DateTime.UtcNow
                }
            };

            var messageJson = System.Text.Json.JsonSerializer.Serialize(testEvent);
            var body = System.Text.Encoding.UTF8.GetBytes(messageJson);

            // Publish with exact routing key that MngKeeper uses
            channel.BasicPublish(
                exchange: _settings.RabbitMQ.ExchangeName,
                routingKey: "system.mngkeeper.domain.created",
                basicProperties: null,
                body: body);

            _logger.LogInformation("Test domain created event published. RoutingKey: system.mngkeeper.domain.created, Exchange: {Exchange}", 
                _settings.RabbitMQ.ExchangeName);

            return Ok(new
            {
                success = true,
                message = "Test domain created event published",
                routingKey = "system.mngkeeper.domain.created",
                exchange = _settings.RabbitMQ.ExchangeName,
                eventId = testEvent.eventId,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing test domain event");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get system event listener queue status
    /// </summary>
    [HttpGet("system-queue-status")]
    public IActionResult GetSystemQueueStatus()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.RabbitMQ.Host,
                Port = _settings.RabbitMQ.Port,
                UserName = _settings.RabbitMQ.Username,
                Password = _settings.RabbitMQ.Password,
                VirtualHost = _settings.RabbitMQ.VirtualHost
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var queueName = "mnghub.system.listener";
            var queueInfo = channel.QueueDeclarePassive(queueName);

            return Ok(new
            {
                queueName = queueInfo.QueueName,
                messageCount = queueInfo.MessageCount,
                consumerCount = queueInfo.ConsumerCount,
                exchange = _settings.RabbitMQ.ExchangeName,
                routingKeyPattern = "system.#"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system queue status");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

