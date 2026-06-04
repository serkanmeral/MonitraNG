using MngEngine.Application.Features.EngineConfig;

namespace MngEngine.Application.Interfaces;

/// <summary>
/// Reactor'dan engine config çeker.
/// </summary>
public interface IConfigSyncClient
{
    Task<EngineConfigSyncResult?> GetConfigAsync(string engineId, CancellationToken ct = default);
}
