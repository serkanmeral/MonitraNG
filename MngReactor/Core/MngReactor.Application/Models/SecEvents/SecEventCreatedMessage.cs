namespace MngReactor.Application.Models.SecEvents;

/// <summary>MQ sec_events.created.{domain} minimal gövde.</summary>
public sealed class SecEventCreatedMessage
{
    public required string Domain { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string EventAction { get; init; }
    public string? NetworkSrcIp { get; init; }
    public required string SourceType { get; init; }
    public required string ParserId { get; init; }
}
