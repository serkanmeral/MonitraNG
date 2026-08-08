namespace MngLogCollector.Application.Abstractions.Observations;

/// <summary>Best-effort publish of agent events to <c>monitra.observations</c>.</summary>
public interface IAgentObservationPublisher
{
    Task PublishEventAsync(AgentObservationPayload payload, CancellationToken cancellationToken = default);
}

public sealed class AgentObservationPayload
{
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
    public required string Key { get; init; }
    public double Value { get; init; } = 1;
    public required DateTime Timestamp { get; init; }
    public IReadOnlyDictionary<string, object?> Dimensions { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
