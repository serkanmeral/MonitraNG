using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class LinuxAuthSyslogParserTests
{
    private readonly LinuxAuthSyslogParser _parser = new();

    [Fact]
    public void CanParse_LinuxProduct_ReturnsTrue()
    {
        var ctx = Context("linux_sshd_failed_password.syslog.txt", product: "linux-syslog", type: "endpoint");
        Assert.True(_parser.CanParse(ctx));
    }

    [Fact]
    public void Parse_SshdFailed_MapsLoginFailed()
    {
        var ctx = Context("linux_sshd_failed_password.syslog.txt");
        var parsed = _parser.Parse(ctx);

        Assert.Equal(LinuxAuthSyslogParser.ParserIdValue, parsed.ParserId);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("admin", parsed.ActorUser);
        Assert.Equal("192.168.50.22", parsed.NetworkSrcIp);
        Assert.Equal("endpoint", parsed.SourceType);
    }

    [Fact]
    public void Parse_SshdAccepted_MapsLoginSuccess()
    {
        var ctx = Context("linux_sshd_accepted_password.syslog.txt");
        var parsed = _parser.Parse(ctx);

        Assert.Equal("login_success", parsed.EventAction);
        Assert.Equal("success", parsed.EventOutcome);
        Assert.Equal("deploy", parsed.ActorUser);
        Assert.Equal("10.20.30.40", parsed.NetworkSrcIp);
    }

    [Fact]
    public void Parse_SudoDenied_MapsPrivilegeDenied()
    {
        var ctx = Context("linux_sudo_denied.syslog.txt");
        var parsed = _parser.Parse(ctx);

        Assert.Equal("privilege_denied", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("deploy", parsed.ActorUser);
        Assert.Null(parsed.NetworkSrcIp);
    }

    [Fact]
    public void CanParse_SshdSession_GenericSyslogProduct_ReturnsTrue()
    {
        var raw = "<38>Jun  5 16:08:18 monitrang sshd-session[141463]: Failed password for invalid user probe from 192.168.20.13 port 22 ssh2";
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.Parse("2026-06-05T13:08:18Z").ToUniversalTime(),
            Source = new SecEventSourceInfo { Type = "firewall", Product = "generic-syslog", Host = "monitrang" },
            Raw = System.Text.Json.JsonSerializer.SerializeToElement(raw)
        };
        Assert.True(_parser.CanParse(ctx));
        var parsed = _parser.Parse(ctx);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("probe", parsed.ActorUser);
        Assert.Equal("192.168.20.13", parsed.NetworkSrcIp);
    }

    [Fact]
    public void Registry_ResolvesLinuxBeforeFirewall()
    {
        var registry = SecEventParserTestFactory.CreateRegistry();

        var ctx = Context("linux_sshd_failed_password.syslog.txt", product: "linux-syslog");
        Assert.Equal(LinuxAuthSyslogParser.ParserIdValue, registry.Resolve(ctx).ParserId);
    }

    private static SecEventRawContext Context(
        string fixture,
        string product = "linux-syslog",
        string type = "endpoint",
        string host = "app01") =>
        new()
        {
            ReceivedAt = DateTime.Parse("2026-06-04T12:00:00Z").ToUniversalTime(),
            Source = new SecEventSourceInfo
            {
                Type = type,
                Product = product,
                Host = host
            },
            Raw = System.Text.Json.JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture(fixture))
        };
}
