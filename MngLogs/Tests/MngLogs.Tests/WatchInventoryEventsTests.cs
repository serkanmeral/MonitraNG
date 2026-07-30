using MngLogs.Agent.Runtime;
using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Tests;

public class WatchInventoryEventsTests
{
    [Fact]
    public void Build_sets_metric_fields_and_targets()
    {
        var snap = new[]
        {
            new ServiceWatchSnapshotItem
            {
                Kind = "service",
                Name = "Spooler",
                DisplayName = "Print Spooler",
                Health = "Running",
                StatusText = "Running",
                RestartAllowed = false,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new ServiceWatchSnapshotItem
            {
                Kind = "application",
                Name = "notepad",
                Health = "Missing",
                StatusText = "instances=0/1",
                RestartAllowed = true,
                InstanceCount = 0,
                MinCount = 1,
                UpdatedAtUtc = DateTime.UtcNow
            }
        };

        var item = WatchInventoryEvents.Build(snap, DateTime.UtcNow);

        Assert.Equal("metric", item.Source);
        Assert.Equal("watch.inventory", item.Message);
        Assert.NotNull(item.Fields);
        Assert.Equal("watch.inventory", item.Fields!["metric"]);
        Assert.Equal(1d, Convert.ToDouble(item.Fields["value"]));
        Assert.Equal(2, Convert.ToInt32(item.Fields["count"]));
        Assert.Equal(1, Convert.ToInt32(item.Fields["unhealthyCount"]));
        Assert.Equal(1, Convert.ToInt32(item.Fields["serviceCount"]));
        Assert.Equal(1, Convert.ToInt32(item.Fields["applicationCount"]));
        Assert.Equal("warning", item.Severity);

        var targets = Assert.IsAssignableFrom<System.Collections.IEnumerable>(item.Fields["targets"]);
        Assert.Equal(2, targets.Cast<object>().Count());
    }
}
