namespace MngReactor.Application.Abstractions.Engine;

/// <summary>
/// Engine config sync tetiklemesi için MQTT'ye sync mesajı yayınlar.
/// Topic: monitoring/{domain}/engine/{engineId}/sync
/// </summary>
public interface IMqttSyncPublisher
{
    /// <summary>
    /// Belirtilen engine için sync tetiklemesi yayınlar.
    /// </summary>
    Task PublishSyncAsync(string domain, string engineId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asset değişikliğinde bu asset'i kullanan agent'ların engine'leri için sync yayınlar.
    /// </summary>
    Task PublishSyncForAssetAsync(string domain, string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Engine'e komut göndermek için command topic'e yayınlar.
    /// Topic: monitoring/{domain}/engine/{engineId}/command
    /// İleride kaynağa yazma vb. için kullanılacak.
    /// </summary>
    Task PublishCommandAsync(string domain, string engineId, string payload, CancellationToken cancellationToken = default);
}
