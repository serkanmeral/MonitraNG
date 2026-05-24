namespace MngScheduler.Application.Configuration;

/// <summary>
/// K3 — periyodik directory sync orchestration (MngKeeper POST per active domain).
/// </summary>
public class DirectorySyncOrchestrationSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Overrides <see cref="Actors.MngKeeper"/> when set (e.g. http://mngkeeper:5001).
    /// </summary>
    public string? MngKeeperBaseUrl { get; set; }

    /// <summary>
    /// Log and continue when a domain returns 5xx or network error.
    /// </summary>
    public bool ContinueOnDomainError { get; set; } = true;

    /// <summary>
    /// Prefer domain <see cref="Interfaces.DomainInfo.Name"/> (realm) over Mongo ObjectId when calling Keeper.
    /// </summary>
    public bool UseDomainNameAsRealm { get; set; } = true;
}
