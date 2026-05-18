namespace MngKeeper.Application.Configuration;

/// <summary>
/// Tenant mode configuration. When UseSingleTenant is true, usernames containing @ are not
/// parsed as domain@user; the system resolves domain from the single domain in the database.
/// </summary>
public class TenantSettings
{
    /// <summary>
    /// When true, treat the deployment as single-tenant: do not split username on @,
    /// and resolve domain via the same logic as when only one domain exists in MongoDB.
    /// </summary>
    public bool UseSingleTenant { get; set; }
}
