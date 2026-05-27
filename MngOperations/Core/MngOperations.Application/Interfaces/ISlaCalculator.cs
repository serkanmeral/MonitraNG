namespace MngOperations.Application.Interfaces;

/// <summary>
/// Work item SLA snapshot hesaplama (Faz 1: düz dakika hedefleri, çalışma saati yok).
/// </summary>
public interface ISlaCalculator
{
    Task ApplyOnCreateAsync(
        Dictionary<string, object?> payload,
        string workspaceId,
        string typeId,
        string? priorityId,
        DateTime anchorUtc,
        string token,
        CancellationToken cancellationToken = default);

    Task ApplyOnTransitionAsync(
        Dictionary<string, object?> merged,
        IReadOnlyDictionary<string, object?> existing,
        DateTime nowUtc,
        string token,
        CancellationToken cancellationToken = default);
}
