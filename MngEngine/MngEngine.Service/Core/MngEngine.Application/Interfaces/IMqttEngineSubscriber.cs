using MngEngine.Application.EngineCommand;

namespace MngEngine.Application.Interfaces;

/// <summary>
/// Engine için MQTT subscribe servisi.
/// monitoring/{domain}/engine/{engineId}/sync ve /command topic'lerine abone olur.
/// </summary>
public interface IMqttEngineSubscriber
{
    /// <summary>
    /// MQTT broker'a bağlanıp sync ve command topic'lerine abone olur.
    /// </summary>
    Task StartAsync(string domain, string engineId, CancellationToken ct = default);

    /// <summary>
    /// Bağlantıyı keser.
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// sync topic'ine mesaj geldiğinde tetiklenir (config sync yapılmalı).
    /// </summary>
    event EventHandler? SyncRequested;

    /// <summary>
    /// command topic'ine mesaj geldiğinde tetiklenir (sync, restart vb.).
    /// </summary>
    event EventHandler<MqttCommandReceivedEventArgs>? CommandReceived;

    bool IsConnected { get; }
}
