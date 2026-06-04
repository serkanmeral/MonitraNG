using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class BastionGenericSyslogParserTests
{
    private readonly BastionGenericSyslogParser _parser = new();
    private readonly LinuxAuthSyslogParser _linuxParser = new();

    [Fact]
    public void ParseBastionFailedPassword_MapsLoginFailed()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("bastion_sshd_failed_password.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-04T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo { Type = "bastion", Product = "bastion", Host = "bastion-jump01" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.True(_parser.CanParse(ctx));
        var parsed = _parser.Parse(ctx);

        Assert.Equal(BastionGenericSyslogParser.ParserIdValue, parsed.ParserId);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("admin", parsed.ActorUser);
        Assert.Equal("192.168.50.33", parsed.NetworkSrcIp);
        Assert.Equal("bastion", parsed.SourceType);
    }

    [Fact]
    public void Registry_ResolvesBastionBeforeLinuxWhenTypeIsBastion()
    {
        var registry = new SecEventParserRegistry(
            new WindowsSecurityExtendedParser(),
            SecEventParserTestFactory.CreateWindowsParser(),
            new BastionGenericSyslogParser(),
            new LinuxAuthSyslogParser(),
            new FirewallVendorParser(),
            new FirewallGenericSyslogParser(),
            new UnknownSecEventFallback());

        var rawLine = SiemFixtureHelper.ReadFixture("bastion_sshd_failed_password.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "bastion", Product = "bastion", Host = "bastion-jump01" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.Equal(BastionGenericSyslogParser.ParserIdValue, registry.Resolve(ctx).ParserId);
    }

    [Fact]
    public void CanParse_LinuxEndpoint_UsesLinuxParser()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("linux_sshd_failed_password.syslog.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "endpoint", Product = "linux-syslog", Host = "app01" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.False(_parser.CanParse(ctx));
        Assert.True(_linuxParser.CanParse(ctx));
    }
}
