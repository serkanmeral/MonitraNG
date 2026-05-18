namespace MngHub.Application.Services;

/// <summary>
/// RabbitMQ consumer service interface
/// </summary>
public interface IRabbitMqConsumer
{
    Task ConnectAsync();
    /// <param name="monitraDataEventsDomainName">MngDataGateway tenant exchange: <c>monitra.data.events.{domain}</c> (JWT domain ile aynı; küçük harfe normalize edilir).</param>
    Task SubscribeAsync(
        string connectionId,
        List<string> routingKeys,
        Func<string, object, Task> messageHandler,
        string? monitraDataEventsDomainName = null);
    Task UnsubscribeAsync(string connectionId);
    Task UnsubscribeAllAsync(string connectionId);
    Task<bool> IsSubscribedAsync(string connectionId, string routingKey);
}

