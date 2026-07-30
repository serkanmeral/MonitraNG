using MngLogs.Agent.Contracts;
using MngLogs.Agent.Runtime;

namespace MngLogs.Tests;

public class ServiceWatchOsCorrelationTests
{
    [Fact]
    public void RecordProduced_correlates_eventlog_serviceName_to_snapshot()
    {
        var status = new AgentRuntimeStatus();
        status.UpdateServiceWatchSnapshot(
            "service",
            "Spooler",
            "Running",
            "Running",
            "Print Spooler",
            restartAllowed: false);

        status.RecordProduced(new IngestEventItem
        {
            Id = "1",
            TimestampUtc = DateTime.UtcNow,
            Source = "windows-eventlog",
            SourceProduct = "system-lifecycle",
            Severity = "error",
            Message = "The Print Spooler service terminated unexpectedly.",
            Fields = new Dictionary<string, object?>
            {
                ["eventId"] = 7034,
                ["serviceName"] = "Print Spooler",
                ["event.action"] = "service.os.crash"
            }
        });

        var snap = Assert.Single(status.ServiceWatchSnapshot());
        Assert.Equal(7034, snap.LastOsEventId);
        Assert.Equal("service.os.crash", snap.LastOsEventAction);
        Assert.NotNull(snap.LastOsEventAtUtc);
        Assert.Contains("Print Spooler", snap.LastOsEventMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateServiceWatchSnapshot_preserves_last_os_and_restart()
    {
        var status = new AgentRuntimeStatus();
        status.UpdateServiceWatchSnapshot("service", "Spooler", "NotRunning", "Stopped", "Print Spooler", true);
        status.NoteOsServiceEvent("Spooler", 7031, "service.os.crash", "crash", DateTime.UtcNow);
        status.NoteServiceRestart("Spooler", false, "access denied", 2);

        status.UpdateServiceWatchSnapshot("service", "Spooler", "Running", "Running", "Print Spooler", true);

        var snap = Assert.Single(status.ServiceWatchSnapshot());
        Assert.Equal(7031, snap.LastOsEventId);
        Assert.Equal(false, snap.LastRestartOk);
        Assert.Equal(2, snap.RestartAttemptCount);
        Assert.Equal("Running", snap.Health);
    }
}
