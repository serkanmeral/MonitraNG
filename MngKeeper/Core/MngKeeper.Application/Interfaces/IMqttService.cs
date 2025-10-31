namespace MngKeeper.Application.Interfaces
{
    public interface IMqttService
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task PublishAsync(string topic, string payload, int qos = 1, bool retain = false);
        Task PublishAsync(string topic, object payload, int qos = 1, bool retain = false);
        Task SubscribeAsync(string topic, int qos = 1);
        Task UnsubscribeAsync(string topic);
        Task<bool> IsConnectedAsync();
        Task<List<string>> GetSubscribedTopicsAsync();
        event EventHandler<MqttMessageReceivedEventArgs> MessageReceived;
        event EventHandler<MqttConnectionEventArgs> ConnectionStateChanged;
    }

    public class MqttMessageReceivedEventArgs : EventArgs
    {
        public string Topic { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public int Qos { get; set; } = 1;
        public bool Retain { get; set; } = false;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ClientId { get; set; }
    }

    public class MqttConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string? Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class MqttDeviceMessage
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? CorrelationId { get; set; }
    }

    public class MqttDeviceCommand
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public object Parameters { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? CorrelationId { get; set; }
    }
}
