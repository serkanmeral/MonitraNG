namespace MngEngine.Application.Interfaces;

/// <summary>
/// Toplama hatalarını saklar. CollectorJob exception'ları buffer'a ekler;
/// EngineStatusJob periyodik olarak alıp Reactor'a gönderir.
/// </summary>
public interface IEngineErrorBuffer
{
    /// <summary>Hatayı buffer'a ekler (max ~100, FIFO).</summary>
    void Add(string assetId, string? agentId, string errorCode, string message);

    /// <summary>Son N hatayı döner (kopya; buffer değişmez).</summary>
    IReadOnlyList<EngineErrorEntry> GetRecent(int count = 50);
}

public record EngineErrorEntry(
    string AssetId,
    string AgentId,
    string ErrorCode,
    string Message,
    DateTime OccurredAt);
