using MngReactor.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Application.Services
{
    public class MqttAppService
    {
        private readonly IMqttService _mqttService;

        public MqttAppService(IMqttService mqttService)
        {
            _mqttService = mqttService;
            _mqttService.MessageReceived += OnMessageReceived;
        }

        public async Task InitializeAsync()
        {
            await _mqttService.ConnectAsync();
            await _mqttService.SubscribeAsync("MNG/collect/#");
        }

        public async Task SendMessageAsync(string topic, string message)
        {
            await _mqttService.PublishAsync(topic, message);
        }

        private void OnMessageReceived(object sender, MqttMessageReceivedEventArgs e)
        {
            // Mesaj alındığında yapılacak işlemler

            Console.WriteLine($"Received message: {e.Payload} on topic: {e.Topic}");
        }
    }
}
