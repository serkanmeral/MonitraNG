using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MngKeeper.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace MngKeeper.Infrastructure.Services
{
    public class RabbitMqService : IRabbitMqService, IDisposable
    {
        private readonly ILogger<RabbitMqService> _logger;
        private readonly IConfiguration _configuration;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly Dictionary<string, IModel> _consumerChannels = new();
        private readonly object _lockObject = new();

        public RabbitMqService(ILogger<RabbitMqService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_connection?.IsOpen == true)
                {
                    _logger.LogInformation("RabbitMQ connection already established");
                    return;
                }

                var factory = new ConnectionFactory
                {
                    HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                    Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
                    UserName = _configuration["RabbitMQ:Username"] ?? "guest",
                    Password = _configuration["RabbitMQ:Password"] ?? "guest",
                    VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/"
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Enable publisher confirms
                _channel.ConfirmSelect();

                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_channel?.IsOpen == true)
                {
                    _channel.Close();
                    _channel.Dispose();
                }

                if (_connection?.IsOpen == true)
                {
                    _connection.Close();
                    _connection.Dispose();
                }

                // Close all consumer channels
                foreach (var consumerChannel in _consumerChannels.Values)
                {
                    if (consumerChannel.IsOpen)
                    {
                        consumerChannel.Close();
                        consumerChannel.Dispose();
                    }
                }
                _consumerChannels.Clear();

                _logger.LogInformation("RabbitMQ connection closed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing RabbitMQ connection");
            }
        }

        public async Task PublishAsync(string exchange, string routingKey, object message)
        {
            var jsonMessage = JsonSerializer.Serialize(message);
            await PublishAsync(exchange, routingKey, jsonMessage);
        }

        public async Task PublishAsync(string exchange, string routingKey, string message)
        {
            try
            {
                await EnsureConnectionAsync();

                // Ensure exchange exists
                await CreateExchangeAsync(exchange);

                var body = Encoding.UTF8.GetBytes(message);
                var properties = _channel!.CreateBasicProperties();
                properties.Persistent = true;
                properties.MessageId = Guid.NewGuid().ToString();
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                _channel.BasicPublish(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);

                // Wait for publisher confirmation
                if (_channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogInformation("Message published successfully to exchange: {Exchange}, routing key: {RoutingKey}", exchange, routingKey);
                }
                else
                {
                    _logger.LogWarning("Message publish confirmation not received for exchange: {Exchange}, routing key: {RoutingKey}", exchange, routingKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to exchange: {Exchange}, routing key: {RoutingKey}", exchange, routingKey);
                throw;
            }
        }

        public async Task SubscribeAsync(string queue, string exchange, string routingKey, Func<string, Task> messageHandler)
        {
            try
            {
                await EnsureConnectionAsync();

                // Create consumer channel
                var consumerChannel = _connection!.CreateModel();
                _consumerChannels[queue] = consumerChannel;

                // Ensure exchange and queue exist
                await CreateExchangeAsync(exchange);
                await CreateQueueAsync(queue);
                await BindQueueAsync(queue, exchange, routingKey);

                var consumer = new EventingBasicConsumer(consumerChannel);
                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var routingKeyReceived = ea.RoutingKey;

                        _logger.LogInformation("Message received from queue: {Queue}, routing key: {RoutingKey}", queue, routingKeyReceived);

                        await messageHandler(message);

                        // Acknowledge the message
                        consumerChannel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue: {Queue}", queue);
                        // Reject the message and requeue it
                        consumerChannel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                consumerChannel.BasicConsume(
                    queue: queue,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Subscribed to queue: {Queue}, exchange: {Exchange}, routing key: {RoutingKey}", queue, exchange, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to queue: {Queue}", queue);
                throw;
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            return _connection?.IsOpen == true && _channel?.IsOpen == true;
        }

        public async Task CreateExchangeAsync(string exchange, string exchangeType = "topic")
        {
            try
            {
                await EnsureConnectionAsync();
                _channel!.ExchangeDeclare(exchange, exchangeType, durable: true, autoDelete: false);
                _logger.LogInformation("Exchange created: {Exchange}, type: {ExchangeType}", exchange, exchangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create exchange: {Exchange}", exchange);
                throw;
            }
        }

        public async Task CreateQueueAsync(string queue)
        {
            try
            {
                await EnsureConnectionAsync();
                _channel!.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
                _logger.LogInformation("Queue created: {Queue}", queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create queue: {Queue}", queue);
                throw;
            }
        }

        public async Task BindQueueAsync(string queue, string exchange, string routingKey)
        {
            try
            {
                await EnsureConnectionAsync();
                _channel!.QueueBind(queue, exchange, routingKey);
                _logger.LogInformation("Queue bound: {Queue} -> {Exchange} ({RoutingKey})", queue, exchange, routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind queue: {Queue} to exchange: {Exchange}", queue, exchange);
                throw;
            }
        }

        private async Task EnsureConnectionAsync()
        {
            if (_connection?.IsOpen != true || _channel?.IsOpen != true)
            {
                await ConnectAsync();
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            GC.SuppressFinalize(this);
        }
    }
}
