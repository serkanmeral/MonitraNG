using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class FirewallVendorParserTests
{
    private readonly FirewallVendorParser _parser = new();

    [Fact]
    public void ParseFortigateDeny_MapsExpectedFields()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("fortigate_traffic_deny.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo
            {
                Type = "firewall",
                Product = "fortigate",
                Host = "FGT-ODAK"
            },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.True(_parser.CanParse(ctx));
        var parsed = _parser.Parse(ctx);

        Assert.Equal(FirewallVendorParser.ParserIdValue, parsed.ParserId);
        Assert.Equal("denied_flow", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("203.0.113.5", parsed.NetworkSrcIp);
        Assert.Equal("10.0.0.10", parsed.NetworkDstIp);
        Assert.Equal(445, parsed.NetworkDstPort);
        Assert.Equal("tcp", parsed.NetworkProtocol);
        Assert.Equal("firewall", parsed.SourceType);
        Assert.Equal("fortigate", parsed.SourceProduct);
    }

    [Fact]
    public void ParseFortigateAllow_MapsAllowedFlow()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("fortigate_traffic_allow.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo { Type = "firewall", Product = "fortigate", Host = "FGT-ODAK" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        var parsed = _parser.Parse(ctx);

        Assert.Equal("allowed_flow", parsed.EventAction);
        Assert.Equal("success", parsed.EventOutcome);
        Assert.Equal("10.0.0.10", parsed.NetworkDstIp);
        Assert.Equal(443, parsed.NetworkDstPort);
    }

    [Fact]
    public void ParseFortigateConfigChange_MapsRuleChangeAndActor()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("fortigate_config_change.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo { Type = "firewall", Product = "fortigate", Host = "FGT-ODAK" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        var parsed = _parser.Parse(ctx);

        Assert.Equal("rule_change", parsed.EventAction);
        Assert.Equal("netadmin", parsed.ActorUser);
        Assert.Contains("firewall.policy", parsed.Raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanParse_SniffsFortigateFormatWithoutProductHint()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("fortigate_traffic_deny.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "firewall", Product = "generic-syslog", Host = "fw-sniff" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.True(_parser.CanParse(ctx));
    }
}
