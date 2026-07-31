namespace MngScheduler.Application.Constants;

/// <summary>
/// Well-known system job identifiers (Mongo @scheduled_jobs.jobId).
/// </summary>
public static class SystemJobIds
{
    /// <summary>
    /// Per-domain Keycloak → Mongo directory sync via MngKeeper (K3).
    /// </summary>
    public const string DirectorySyncAllDomains = "system-directory-sync-all-domains";

    /// <summary>
    /// SIEM Discovery AD computer pull via MngLogCollector POST /api/v1/discovery/sync.
    /// </summary>
    public const string SiemDiscoveryAdSync = "system-siem-discovery-ad-sync";

    public static bool IsDirectorySyncOrchestration(string jobId) =>
        string.Equals(jobId, DirectorySyncAllDomains, StringComparison.OrdinalIgnoreCase);
}
