using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventFlowBaselineEnricherTests
{
    private static ParsedSecEvent FirewallFlow(string action = "denied_flow") =>
        new()
        {
            Timestamp = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            EventAction = action,
            NetworkSrcIp = "10.1.1.1",
            NetworkDstIp = "10.2.2.2",
            SourceType = "firewall",
            SourceProduct = "generic-syslog",
            SourceHost = "fw01",
            ParserId = "firewall-generic-syslog",
            Raw = "DENY SRC=10.1.1.1 DST=10.2.2.2",
            RawPreview = "DENY SRC=10.1.1.1 DST=10.2.2.2"
        };

    [Fact]
    public async Task EnrichAsync_NewPair_KeepsOriginalActionAndSetsEmitFlag()
    {
        var store = new Mock<ISecEventFlowBaselineStore>();
        store.Setup(s => s.ApplyFlowPairAsync(
                "odak", "10.1.1.1", "10.2.2.2", "denied_flow", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecEventFlowBaselineApplyResult(true));

        var result = await SecEventFlowBaselineEnricher.EnrichAsync(
            FirewallFlow(), "odak", store.Object, CancellationToken.None);

        Assert.Equal("denied_flow", result.Parsed.EventAction);
        Assert.True(result.EmitNewFlowObservation);
    }

    [Fact]
    public async Task EnrichAsync_NonFlowAction_SkipsBaseline()
    {
        var store = new Mock<ISecEventFlowBaselineStore>(MockBehavior.Strict);
        var parsed = new ParsedSecEvent
        {
            Timestamp = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            EventAction = "login_failed",
            SourceType = "ad",
            SourceProduct = "windows",
            ParserId = "windows-security",
            Raw = "{}",
            RawPreview = "{}"
        };

        var result = await SecEventFlowBaselineEnricher.EnrichAsync(
            parsed, "odak", store.Object, CancellationToken.None);

        Assert.Equal("login_failed", result.Parsed.EventAction);
        Assert.False(result.EmitNewFlowObservation);
    }

    [Fact]
    public async Task EnrichAsync_MissingIp_SkipsBaseline()
    {
        var store = new Mock<ISecEventFlowBaselineStore>(MockBehavior.Strict);
        var parsed = new ParsedSecEvent
        {
            Timestamp = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            EventAction = "denied_flow",
            NetworkSrcIp = "10.1.1.1",
            NetworkDstIp = null,
            SourceType = "firewall",
            SourceProduct = "generic-syslog",
            SourceHost = "fw01",
            ParserId = "firewall-generic-syslog",
            Raw = "DENY",
            RawPreview = "DENY"
        };

        var result = await SecEventFlowBaselineEnricher.EnrichAsync(
            parsed, "odak", store.Object, CancellationToken.None);

        Assert.Equal("denied_flow", result.Parsed.EventAction);
        Assert.False(result.EmitNewFlowObservation);
    }
}
