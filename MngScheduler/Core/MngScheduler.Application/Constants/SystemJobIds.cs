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

    public static bool IsDirectorySyncOrchestration(string jobId) =>
        string.Equals(jobId, DirectorySyncAllDomains, StringComparison.OrdinalIgnoreCase);
}
