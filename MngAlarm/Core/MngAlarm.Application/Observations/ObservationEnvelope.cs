namespace MngAlarm.Application.Observations;

public sealed class ObservationEnvelope
{
    public string DomainId { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "metric";
    public string Key { get; set; } = string.Empty;
    public double? Value { get; set; }
    public Dictionary<string, object?> Dimensions { get; set; } = new();
}

public sealed class AlarmEventMessage
{
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
    public required string EventType { get; init; }
    public required string AlarmId { get; init; }
    public required string RuleId { get; init; }
    public int Severity { get; init; }
    public required string DedupKey { get; init; }
    public Dictionary<string, object?> Context { get; init; } = new();
    public required string CorrelationId { get; init; }
    public required string EventId { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
