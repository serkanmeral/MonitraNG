using MngLogCollector.Application.Services.Ingest;
using Xunit;

namespace MngLogCollector.Tests;

public sealed class AgentSecEventActionNormalizerTests
{
    [Theory]
    [InlineData("rdp-session", "24", "rdp.disconnect")]
    [InlineData("rdp-session", "25", "rdp.reconnect")]
    [InlineData("rdp-session", "21", "rdp.logon")]
    [InlineData("rdp-session", "23", "rdp.logoff")]
    [InlineData("rdp-sessions", "24", "rdp.disconnect")]
    public void TryNormalize_RdpPackage_MapsCodes(string product, string code, string expected) =>
        Assert.Equal(expected, AgentSecEventActionNormalizer.TryNormalize(product, "windows-eventlog", code, null));

    [Fact]
    public void TryNormalize_NonRdp_ReturnsNull() =>
        Assert.Null(AgentSecEventActionNormalizer.TryNormalize("application-signals", "windows-eventlog", "1000", "boom"));
}
