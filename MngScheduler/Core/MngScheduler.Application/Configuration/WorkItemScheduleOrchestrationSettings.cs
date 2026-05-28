namespace MngScheduler.Application.Configuration;

/// <summary>
/// SW-3 — Operation Core zamanlanmış work item: Keeper token → MngOperations from-origin.
/// </summary>
public class WorkItemScheduleOrchestrationSettings
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Overrides <see cref="Actors.MngKeeper"/> when set (e.g. http://mngkeeper:5001).
    /// </summary>
    public string? MngKeeperBaseUrl { get; set; }

    /// <summary>
    /// Keeper direkt: /api/auth/token. Gateway: /keeper/api/auth/token.
    /// </summary>
    public string KeeperTokenPath { get; set; } = "/api/auth/token";

    /// <summary>
    /// MO from-origin URL (gateway veya doğrudan MO).
    /// </summary>
    public string GatewayOperationsFromOrigin { get; set; } = string.Empty;

    /// <summary>
    /// MO execute URL şablonu. {scheduleId} zorunlu. SW-3c cron tetik hedefi.
    /// </summary>
    public string ExecuteEndpointTemplate { get; set; } =
        "http://mngoperations:5086/api/v1/work-item-schedules/{scheduleId}/execute";

    public WorkItemScheduleServiceAccountSettings ServiceAccount { get; set; } = new();
}

public class WorkItemScheduleServiceAccountSettings
{
    public string DomainName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
