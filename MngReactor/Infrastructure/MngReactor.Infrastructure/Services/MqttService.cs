using MngReactor.Domain.Interfaces;
using MQTTnet;
using MQTTnet.Client;

using System.Text;


namespace MngReactor.Infrastructure.Services
{
    public class MqttService : IMqttService
    {
        private readonly IMqttClient _mqttClient;
        private readonly MqttClientOptions _mqttOptions;

        public event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;

        public MqttService(string broker, int port, string username, string password)
        {
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            _mqttOptions = new MqttClientOptionsBuilder()
                .WithClientId(Guid.NewGuid().ToString())
                .WithTcpServer(broker, port)
                .WithCredentials(username, password)
                .WithCleanSession()
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += HandleReceivedApplicationMessage;
            _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;
            _mqttClient.ConnectedAsync += HandleConnectedAsync;
        }

        private async Task HandleConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            Console.WriteLine("Connected to MQTT broker.");

            await Task.CompletedTask;
        }

        private async Task HandleReceivedApplicationMessage(MqttApplicationMessageReceivedEventArgs e)
        {
            var args = new MqttMessageReceivedEventArgs
            {
                Topic = e.ApplicationMessage.Topic,
                Payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
            };
            MessageReceived?.Invoke(this, args);
            await Task.CompletedTask;
        }

        private async Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine("Disconnected from MQTT broker. Trying to reconnect...");
            await Task.Delay(TimeSpan.FromSeconds(5)); // 5 saniye bekle
            try
            {
                await _mqttClient.ConnectAsync(_mqttOptions);
                Console.WriteLine("Reconnected to MQTT broker.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reconnection failed: {ex.Message}");
            }
        }

        public async Task ConnectAsync()
        {
            await _mqttClient.ConnectAsync(_mqttOptions);
        }

        public async Task PublishAsync(string topic, string payload)
        {
            if (!_mqttClient.IsConnected)
                await ConnectAsync();

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message);
        }

        public async Task SubscribeAsync(string topic)
        {
            var topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
            await _mqttClient.SubscribeAsync(topicFilter);
        }
    }
}
