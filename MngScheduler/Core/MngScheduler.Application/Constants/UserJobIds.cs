namespace MngScheduler.Application.Constants;

/// <summary>
/// Operation Core zamanlanmış WI user job kimlikleri (SW-3b/3c).
/// </summary>
public static class UserJobIds
{
    public const string WorkItemSchedulePrefix = "oc-schedule-";
    public const string SlaBreachScanPrefix = "oc-sla-scan-";
    public const string AlarmValidationPrefix = "alarm-validation-";

    public static bool IsWorkItemSchedule(string? jobId) =>
        !string.IsNullOrWhiteSpace(jobId)
        && jobId.StartsWith(WorkItemSchedulePrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsSlaBreachScan(string? jobId) =>
        !string.IsNullOrWhiteSpace(jobId)
        && jobId.StartsWith(SlaBreachScanPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsAlarmValidation(string? jobId) =>
        !string.IsNullOrWhiteSpace(jobId)
        && jobId.StartsWith(AlarmValidationPrefix, StringComparison.OrdinalIgnoreCase);

    public static string? TryGetScheduleDataId(string? jobId)
    {
        if (!IsWorkItemSchedule(jobId))
            return null;

        var id = jobId![WorkItemSchedulePrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    public static string? TryGetSlaBreachScanWorkspaceId(string? jobId)
    {
        if (!IsSlaBreachScan(jobId))
            return null;

        var id = jobId![SlaBreachScanPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    public static string? TryGetAlarmValidationDomainName(string? jobId)
    {
        if (!IsAlarmValidation(jobId))
            return null;

        var name = jobId![AlarmValidationPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
