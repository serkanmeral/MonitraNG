using MngLogs.Agent.Metrics;

namespace MngLogs.Tests;

public class HostNetworkAddressesTests
{
    [Fact]
    public void PickPrimary_PrefersPrivateLan()
    {
        var primary = HostNetworkAddresses.PickPrimary(["8.8.8.8", "192.168.20.55", "1.1.1.1"]);
        Assert.Equal("192.168.20.55", primary);
    }

    [Fact]
    public void PickPrimary_Empty_ReturnsNull()
    {
        Assert.Null(HostNetworkAddresses.PickPrimary([]));
    }
}
