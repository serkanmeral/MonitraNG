namespace MngReactor.Application.Features.Mqtt;

public sealed record MqttPublishRequest
{
    public required string Topic { get; init; }
    public required string Message { get; init; }
}

public sealed record MqttPublishResponse
{
    public bool Published { get; init; }
    public required string Topic { get; init; }
}
