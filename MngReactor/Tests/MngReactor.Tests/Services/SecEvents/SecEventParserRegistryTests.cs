using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventParserRegistryTests
{
    private readonly SecEventParserRegistry _registry = new(
        new WindowsSecurityParser(),
        new FirewallGenericSyslogParser(),
        new UnknownSecEventFallback());

    [Fact]
    public void S2_4_RoutesWindowsProductToWindowsParser()
    {
        using var doc = JsonDocument.Parse(SiemFixtureHelper.ReadFixture("windows_4625_failed_logon.json"));
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows" },
            Raw = doc.RootElement.Clone()
        };

        var parser = _registry.Resolve(ctx);
        Assert.Equal(WindowsSecurityParser.ParserIdValue, parser.ParserId);
    }

    [Fact]
    public void S2_4_RoutesGenericSyslogToFirewallParser()
    {
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "firewall", Product = "generic-syslog" },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("firewall_deny.syslog.txt"))
        };

        var parser = _registry.Resolve(ctx);
        Assert.Equal(FirewallGenericSyslogParser.ParserIdValue, parser.ParserId);
    }

    [Fact]
    public void S2_4_UnknownFormatUsesFallback()
    {
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "other", Product = "other" },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("unparseable_01.txt"))
        };

        var parser = _registry.Resolve(ctx);
        Assert.Equal(UnknownSecEventFallback.ParserIdValue, parser.ParserId);
    }
}
