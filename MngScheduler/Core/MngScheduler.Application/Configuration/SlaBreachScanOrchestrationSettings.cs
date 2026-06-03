namespace MngScheduler.Application.Configuration;

/// <summary>
/// Operation Core SLA breach scan cron → Keeper token → MO scan-breaches.
/// </summary>
public class SlaBreachScanOrchestrationSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// MO scan URL şablonu. {workspaceId} zorunlu.
    /// </summary>
    public string ScanEndpointTemplate { get; set; } =
        "http://mngoperations:5086/api/v1/sla/scan-breaches?workspaceId={workspaceId}";
}
