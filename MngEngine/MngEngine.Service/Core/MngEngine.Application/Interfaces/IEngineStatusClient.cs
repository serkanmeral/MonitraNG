namespace MngEngine.Application.Interfaces;

/// <summary>
/// Engine status (heartbeat + hata raporu) Reactor'a gönderir.
/// POST /api/v1/engine/status
/// </summary>
public interface IEngineStatusClient
{
    Task<bool> SendStatusAsync(EngineStatusPayload payload, CancellationToken ct = default);
}

public record EngineStatusPayload(
    string EngineId,
    string Domain,
    DateTime Timestamp,
    string Health,
    IReadOnlyList<EngineStatusErrorItem> Errors,
    int? QueueDepth = null,
    int? AssetCount = null,
    string? HostAddress = null);

public record EngineStatusErrorItem(
    string AssetId,
    string AgentId,
    string ErrorCode,
    string Message,
    DateTime OccurredAt);
