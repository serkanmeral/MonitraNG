namespace MngHub.Application.Services;

/// <summary>
/// RabbitMQ consumer service interface
/// </summary>
public interface IRabbitMqConsumer
{
    Task ConnectAsync();
    Task SubscribeAsync(string connectionId, List<string> routingKeys, Func<string, object, Task> messageHandler);
    Task UnsubscribeAsync(string connectionId);
    Task UnsubscribeAllAsync(string connectionId);
    Task<bool> IsSubscribedAsync(string connectionId, string routingKey);
}

