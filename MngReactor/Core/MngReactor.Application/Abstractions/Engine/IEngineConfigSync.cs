namespace MngReactor.Application.Abstractions.Engine;

/// <summary>
/// Engine için config sync - mon_agents, mon_assets, period, schedule birleştirir.
/// </summary>
public interface IEngineConfigSync
{
    Task<EngineConfigSyncResult?> GetConfigAsync(string engineId, string domain, string accessToken, CancellationToken cancellationToken = default);
}
