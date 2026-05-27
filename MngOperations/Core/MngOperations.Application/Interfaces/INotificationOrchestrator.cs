namespace MngOperations.Application.Interfaces;

public interface INotificationOrchestrator
{
    Task DispatchWorkItemEventAsync(
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default);

    Task DispatchRuleSideEffectAsync(
        string effectType,
        IReadOnlyDictionary<string, object?> payload,
        NotificationDispatchRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationDispatchRequest
{
    public required string EventType { get; init; }
    public required string WorkspaceId { get; init; }
    public required string WorkItemId { get; init; }
    public required string WorkItemKey { get; init; }
    public required IReadOnlyDictionary<string, object?> WorkItem { get; init; }
    public string? TypeId { get; init; }
    public string? BoardId { get; init; }
    public string? TransitionKey { get; init; }
    public string? FromStateId { get; init; }
    public string? ToStateId { get; init; }
    public string? Actor { get; init; }
    public required string Token { get; init; }
}
