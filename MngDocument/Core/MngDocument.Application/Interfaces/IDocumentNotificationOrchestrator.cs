using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Application.Interfaces;

/// <summary>D-N — document lifecycle notifications (best-effort; never fails the caller).</summary>
public interface IDocumentNotificationOrchestrator
{
    /// <summary>Fire-and-forget style notify after a successful document generation.</summary>
    Task NotifyDocumentGeneratedAsync(
        GenerateDocumentResultDto result,
        IReadOnlyList<string>? extraRecipients = null,
        CancellationToken ct = default);
}
