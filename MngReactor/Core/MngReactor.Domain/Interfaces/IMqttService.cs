using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MngReactor.Domain.Interfaces
{
    public interface IMqttService
    {
        Task ConnectAsync();
        Task PublishAsync(string topic, string payload);
        Task SubscribeAsync(string topic);
        event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; }
        public string Payload { get; set; }
    }
}
