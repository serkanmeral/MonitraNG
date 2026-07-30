using MngLogs.Agent.Contracts;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.ServiceWatch;

/// <summary>Builds metric-style watch.inventory events for central (Mng.Ui) dashboards.</summary>
public static class WatchInventoryEvents
{
    public const string MetricName = "watch.inventory";
    public const string Action = "watch.inventory";

    public static IngestEventItem Build(IReadOnlyList<ServiceWatchSnapshotItem> snapshot, DateTime utcNow)
    {
        var targets = snapshot
            .OrderBy(x => x.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new Dictionary<string, object?>
            {
                ["kind"] = x.Kind,
                ["name"] = x.Name,
                ["displayName"] = x.DisplayName,
                ["health"] = x.Health,
                ["statusText"] = x.StatusText,
                ["restartAllowed"] = x.RestartAllowed,
                ["instanceCount"] = x.InstanceCount,
                ["minCount"] = x.MinCount,
                ["lastOsEventId"] = x.LastOsEventId,
                ["lastOsEventAction"] = x.LastOsEventAction,
                ["lastOsEventAtUtc"] = x.LastOsEventAtUtc,
                ["lastRestartOk"] = x.LastRestartOk,
                ["lastRestartAtUtc"] = x.LastRestartAtUtc,
                ["restartAttemptCount"] = x.RestartAttemptCount
            })
            .ToList();

        var unhealthy = snapshot.Count(x =>
        {
            if (!Enum.TryParse<ServiceWatchHealth>(x.Health, ignoreCase: true, out var h))
                return false;
            return ServiceWatchTransitions.IsUnhealthy(h);
        });

        return new IngestEventItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = utcNow,
            Source = "metric",
            SourceProduct = "mnglogs-agent",
            Severity = unhealthy > 0 ? "warning" : "info",
            Message = Action,
            Fields = new Dictionary<string, object?>
            {
                ["metric"] = MetricName,
                ["value"] = unhealthy,
                ["event.action"] = Action,
                ["count"] = snapshot.Count,
                ["unhealthyCount"] = unhealthy,
                ["healthyCount"] = Math.Max(0, snapshot.Count - unhealthy),
                ["serviceCount"] = snapshot.Count(x =>
                    string.Equals(x.Kind, "service", StringComparison.OrdinalIgnoreCase)),
                ["applicationCount"] = snapshot.Count(x =>
                    string.Equals(x.Kind, "application", StringComparison.OrdinalIgnoreCase)),
                ["targets"] = targets,
                ["machine"] = Environment.MachineName
            }
        };
    }
}
