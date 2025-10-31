namespace MngKeeper.Application.Interfaces
{
    public interface IRabbitMqService
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        Task PublishAsync(string exchange, string routingKey, object message);
        Task PublishAsync(string exchange, string routingKey, string message);
        Task SubscribeAsync(string queue, string exchange, string routingKey, Func<string, Task> messageHandler);
        Task<bool> IsConnectedAsync();
        Task CreateExchangeAsync(string exchange, string exchangeType = "topic");
        Task CreateQueueAsync(string queue);
        Task BindQueueAsync(string queue, string exchange, string routingKey);
    }

    public class RabbitMqMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public string DomainId { get; set; } = string.Empty;
        public object Data { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? CorrelationId { get; set; }
    }

    public class RabbitMqMessageEventArgs : EventArgs
    {
        public string Exchange { get; set; } = string.Empty;
        public string RoutingKey { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
