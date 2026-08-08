using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Evaluation;

/// <summary>
/// Updates scenario runtime health. Lifecycle status (running/stopped) stays separate;
/// health is a secondary badge (unknown / healthy / warning / error).
/// Auto-disable on error is intentionally not applied here.
/// </summary>
public static class ScenarioHealthTracker
{
    public const int ErrorLevelThreshold = 3;
    public static readonly TimeSpan ErrorWindow = TimeSpan.FromHours(1);

    public static void RecordSuccess(ScenarioRuntimeHealth health, DateTime utcNow)
    {
        health.LastSuccessAt = utcNow;
        health.ConsecutiveErrors = 0;
        health.Level = ScenarioHealthLevels.Healthy;
    }

    public static void RecordError(
        ScenarioRuntimeHealth health,
        DateTime utcNow,
        string code,
        string message,
        string? nodeId = null)
    {
        if (health.WindowStartedAt == null || utcNow - health.WindowStartedAt > ErrorWindow)
        {
            health.WindowStartedAt = utcNow;
            health.ErrorCountWindow = 0;
        }

        health.ErrorCountWindow++;
        health.ConsecutiveErrors++;
        health.LastErrorAt = utcNow;
        health.LastErrorCode = Truncate(code, 120);
        health.LastErrorMessage = Truncate(message, 500);
        health.LastErrorNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : Truncate(nodeId, 120);
        health.Level = health.ConsecutiveErrors >= ErrorLevelThreshold
            ? ScenarioHealthLevels.Error
            : ScenarioHealthLevels.Warning;
    }

    public static void RecordError(
        ScenarioRuntimeHealth health,
        DateTime utcNow,
        Exception exception,
        string? nodeId = null) =>
        RecordError(
            health,
            utcNow,
            exception.GetType().Name,
            exception.Message,
            nodeId);

    public static string ResolveOperationalStatus(
        ScenarioVersionDocument? published,
        ScenarioVersionDocument latest)
    {
        if (published != null)
            return published.Enabled
                ? ScenarioOperationalStatuses.Running
                : ScenarioOperationalStatuses.Stopped;

        if (latest.Status == ScenarioLifecycleStatuses.Archived)
            return ScenarioOperationalStatuses.Archived;

        return ScenarioOperationalStatuses.Draft;
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
