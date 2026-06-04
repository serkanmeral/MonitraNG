using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class FirewallGenericSyslogParserTests
{
    private readonly FirewallGenericSyslogParser _parser = new();

    [Fact]
    public void S2_1_ParseFirewallDeny_MapsExpectedFields()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("firewall_deny.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo
            {
                Type = "firewall",
                Product = "generic-syslog",
                Host = "fw01"
            },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.True(_parser.CanParse(ctx));
        var parsed = _parser.Parse(ctx);

        Assert.Equal(FirewallGenericSyslogParser.ParserIdValue, parsed.ParserId);
        Assert.Equal("denied_flow", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("203.0.113.5", parsed.NetworkSrcIp);
        Assert.Equal("10.0.0.10", parsed.NetworkDstIp);
        Assert.Equal(445, parsed.NetworkDstPort);
        Assert.Equal("tcp", parsed.NetworkProtocol);
        Assert.Equal("firewall", parsed.SourceType);
        Assert.Equal("generic-syslog", parsed.SourceProduct);
        Assert.Contains("DENY", parsed.RawPreview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseFirewallRuleChange_MapsRuleChangeActionAndActor()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("firewall_rule_change.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo
            {
                Type = "firewall",
                Product = "generic-syslog",
                Host = "fw01"
            },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        var parsed = _parser.Parse(ctx);

        Assert.Equal("rule_change", parsed.EventAction);
        Assert.Equal("netadmin", parsed.ActorUser);
        Assert.Equal("192.168.1.1", parsed.NetworkSrcIp);
        Assert.Contains("CONFIG CHANGE", parsed.Raw, StringComparison.OrdinalIgnoreCase);
    }
}
