namespace MngWorkflow.Infrastructure.Utilities;

public static class WorkflowDelayCronHelper
{
    /// <summary>
    /// Quartz 7 alanlı tek seferlik cron: saniye dakika saat gün ay ? yıl (UTC).
    /// </summary>
    public static string ToOneShotCron(DateTime resumeAtUtc)
    {
        var utc = resumeAtUtc.Kind == DateTimeKind.Utc
            ? resumeAtUtc
            : resumeAtUtc.ToUniversalTime();

        return $"{utc.Second} {utc.Minute} {utc.Hour} {utc.Day} {utc.Month} ? {utc.Year}";
    }
}
