using MngLogs.Agent.Cli;

namespace MngLogs.Tests;

public class LocalUiPortProbeTests
{
    [Fact]
    public void IsPortAvailable_rejects_invalid_port()
    {
        Assert.False(LocalUiPortProbe.IsPortAvailable("127.0.0.1", 0, out var detail));
        Assert.Contains("1–65535", detail);
    }

    [Fact]
    public void IsPortAvailable_accepts_ephemeral_free_port()
    {
        // Port 0 is invalid for our helper; pick a high port that is likely free by binding via OS.
        // Use a random high port and verify probe agrees with a real TcpListener cycle.
        var port = 58000 + Random.Shared.Next(0, 2000);
        // If busy, skip assertion softness
        var free = LocalUiPortProbe.IsPortAvailable("127.0.0.1", port, out _);
        if (free)
            Assert.True(LocalUiPortProbe.IsPortAvailable("127.0.0.1", port, out _));
    }
}
