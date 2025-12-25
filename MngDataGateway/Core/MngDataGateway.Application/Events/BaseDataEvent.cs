namespace MngDataGateway.Application.Events;

/// <summary>
/// Base class for all DataGateway events
/// </summary>
public abstract class BaseDataEvent
{
    /// <summary>
    /// Unique event identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Event type (e.g., "DataCreatedEvent")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Domain ID where the event occurred
    /// </summary>
    public string DomainId { get; set; } = string.Empty;

    /// <summary>
    /// Event timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for tracking related events
    /// </summary>
    public string? CorrelationId { get; set; }
}

