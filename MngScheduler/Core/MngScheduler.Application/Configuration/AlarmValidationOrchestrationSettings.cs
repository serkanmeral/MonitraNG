namespace MngScheduler.Application.Configuration;

/// <summary>
/// MngAlarm scheduled validation scan cron → Keeper token → MA validation/run.
/// </summary>
public class AlarmValidationOrchestrationSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Validation URL şablonu. {domainName} zorunlu.
    /// </summary>
    public string ValidationEndpointTemplate { get; set; } =
        "http://mngalarm:5087/api/v1/validation/run";
}
