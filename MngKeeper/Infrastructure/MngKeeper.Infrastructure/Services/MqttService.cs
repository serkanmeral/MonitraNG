using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MngKeeper.Application.Interfaces;
using System.Text.Json;

namespace MngKeeper.Infrastructure.Services
{
    public class MqttService : IMqttService, IDisposable
    {
        private readonly ILogger<MqttService> _logger;
        private readonly IConfiguration _configuration;
        private IMqttClient? _mqttClient;
        private readonly List<string> _subscribedTopics = new();
        private readonly object _lockObject = new();

        public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<MqttConnectionEventArgs>? ConnectionStateChanged;

        public MqttService(ILogger<MqttService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ConnectAsync()
        {
            try
            {
                if (_mqttClient?.IsConnected == true)
                {
                    _logger.LogInformation("MQTT client already connected");
                    return;
                }

                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                // Configure client options
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(
                        _configuration["Mqtt:BrokerHost"] ?? "localhost",
                        int.Parse(_configuration["Mqtt:BrokerPort"] ?? "1883"))
                    .WithCredentials(
                        _configuration["Mqtt:Username"] ?? "monitrang",
                        _configuration["Mqtt:Password"] ?? "!2345qawsedrf")
                    .WithClientId($"MngKeeper_{Guid.NewGuid()}")
                    .WithCleanSession()
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                    .Build();

                // Set up event handlers
                _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
                _mqttClient.ConnectedAsync += OnConnected;
                _mqttClient.DisconnectedAsync += OnDisconnected;

                // Connect to broker
                await _mqttClient.ConnectAsync(options);

                _logger.LogInformation("MQTT client connected successfully to {BrokerHost}:{BrokerPort}", 
                    _configuration["Mqtt:BrokerHost"], _configuration["Mqtt:BrokerPort"]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MQTT broker");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_mqttClient?.IsConnected == true)
                {
                    await _mqttClient.DisconnectAsync();
                    _logger.LogInformation("MQTT client disconnected successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting MQTT client");
            }
        }

        public async Task<bool> IsConnectedAsync()
        {
            return _mqttClient?.IsConnected == true;
        }

        public async Task PublishAsync(string topic, string payload, int qos = 1, bool retain = false)
        {
            try
            {
                await EnsureConnectionAsync();

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                    .WithRetainFlag(retain)
                    .Build();

                await _mqttClient!.PublishAsync(message);

                _logger.LogInformation("Message published to topic: {Topic}, QoS: {Qos}, Retain: {Retain}", topic, qos, retain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to topic: {Topic}", topic);
                throw;
            }
        }

        public async Task PublishAsync(string topic, object payload, int qos = 1, bool retain = false)
        {
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await PublishAsync(topic, jsonPayload, qos, retain);
        }

        public async Task SubscribeAsync(string topic, int qos = 1)
        {
            try
            {
                await EnsureConnectionAsync();

                var subscribeOptions = new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)qos)
                    .Build();

                await _mqttClient!.SubscribeAsync(subscribeOptions);

                lock (_lockObject)
                {
                    if (!_subscribedTopics.Contains(topic))
                    {
                        _subscribedTopics.Add(topic);
                    }
                }

                _logger.LogInformation("Subscribed to topic: {Topic}, QoS: {Qos}", topic, qos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to topic: {Topic}", topic);
                throw;
            }
        }

        public async Task UnsubscribeAsync(string topic)
        {
            try
            {
                if (_mqttClient?.IsConnected == true)
                {
                    await _mqttClient.UnsubscribeAsync(topic);

                    lock (_lockObject)
                    {
                        _subscribedTopics.Remove(topic);
                    }

                    _logger.LogInformation("Unsubscribed from topic: {Topic}", topic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from topic: {Topic}", topic);
                throw;
            }
        }

        public async Task<List<string>> GetSubscribedTopicsAsync()
        {
            lock (_lockObject)
            {
                return new List<string>(_subscribedTopics);
            }
        }

        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var payload = e.ApplicationMessage.PayloadSegment.Count > 0 
                    ? System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)
                    : string.Empty;

                var messageArgs = new MqttMessageReceivedEventArgs
                {
                    Topic = e.ApplicationMessage.Topic,
                    Payload = payload,
                    Qos = (int)e.ApplicationMessage.QualityOfServiceLevel,
                    Retain = e.ApplicationMessage.Retain,
                    Timestamp = DateTime.UtcNow,
                    ClientId = e.ClientId
                };

                _logger.LogInformation("Message received from topic: {Topic}, QoS: {Qos}, Payload length: {PayloadLength}", 
                    messageArgs.Topic, messageArgs.Qos, payload.Length);

                MessageReceived?.Invoke(this, messageArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received message");
            }
        }

        private async Task OnConnected(MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation("MQTT client connected successfully");
            
            ConnectionStateChanged?.Invoke(this, new MqttConnectionEventArgs
            {
                IsConnected = true,
                Timestamp = DateTime.UtcNow
            });

            // Resubscribe to topics after reconnection
            var topics = await GetSubscribedTopicsAsync();
            foreach (var topic in topics)
            {
                try
                {
                    await SubscribeAsync(topic);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resubscribe to topic: {Topic}", topic);
                }
            }
        }

        private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            _logger.LogWarning("MQTT client disconnected. Reason: {Reason}", e.Reason);
            
            ConnectionStateChanged?.Invoke(this, new MqttConnectionEventArgs
            {
                IsConnected = false,
                Reason = e.Reason.ToString(),
                Timestamp = DateTime.UtcNow
            });

            // Attempt to reconnect after a delay
            if (e.Reason == MqttClientDisconnectReason.NormalDisconnection)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                await ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to MQTT broker");
            }
        }

        private async Task EnsureConnectionAsync()
        {
            if (_mqttClient?.IsConnected != true)
            {
                await ConnectAsync();
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _mqttClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
