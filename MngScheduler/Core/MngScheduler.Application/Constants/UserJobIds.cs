namespace MngScheduler.Application.Constants;

/// <summary>
/// Operation Core zamanlanmış WI user job kimlikleri (SW-3b/3c).
/// </summary>
public static class UserJobIds
{
    public const string WorkItemSchedulePrefix = "oc-schedule-";

    public static bool IsWorkItemSchedule(string? jobId) =>
        !string.IsNullOrWhiteSpace(jobId)
        && jobId.StartsWith(WorkItemSchedulePrefix, StringComparison.OrdinalIgnoreCase);

    public static string? TryGetScheduleDataId(string? jobId)
    {
        if (!IsWorkItemSchedule(jobId))
            return null;

        var id = jobId![WorkItemSchedulePrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
