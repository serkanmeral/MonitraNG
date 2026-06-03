using MngReactor.Domain.Interfaces;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// Test icin MQTT baglantisi yapmayan no-op servis.
/// </summary>
public class NoopMqttService : IMqttService
{
    public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

    public Task ConnectAsync() => Task.CompletedTask;
    public Task PublishAsync(string topic, string payload) => Task.CompletedTask;
    public Task SubscribeAsync(string topic) => Task.CompletedTask;
}
