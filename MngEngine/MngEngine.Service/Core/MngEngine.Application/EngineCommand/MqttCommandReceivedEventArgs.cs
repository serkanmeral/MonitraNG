namespace MngEngine.Application.EngineCommand;

/// <summary>
/// MQTT command topic'inden gelen komut için event argümanları.
/// Payload örnek: { "command": "sync" } veya "sync"
/// </summary>
public class MqttCommandReceivedEventArgs : EventArgs
{
    public string Command { get; init; } = "";
    public string RawPayload { get; init; } = "";
}
