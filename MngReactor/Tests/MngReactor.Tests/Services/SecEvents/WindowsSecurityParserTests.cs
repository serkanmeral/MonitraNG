using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class WindowsSecurityParserTests
{
    private readonly WindowsSecurityParser _parser = SecEventParserTestFactory.CreateWindowsParser();

    [Fact]
    public void S2_2_ParseWindows4625_MapsExpectedFields()
    {
        var json = SiemFixtureHelper.ReadFixture("windows_4625_failed_logon.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo
            {
                Type = "ad",
                Product = "windows",
                Host = "dc01"
            },
            Raw = doc.RootElement.Clone()
        };

        Assert.True(_parser.CanParse(ctx));
        var parsed = _parser.Parse(ctx);

        Assert.Equal(WindowsSecurityParser.ParserIdValue, parsed.ParserId);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("4625", parsed.EventCode);
        Assert.Equal("admin", parsed.ActorUser);
        Assert.Equal("192.168.1.50", parsed.NetworkSrcIp);
        Assert.Equal(DateTime.Parse("2026-06-03T14:00:02Z").ToUniversalTime(), parsed.Timestamp);
        Assert.Equal("ad", parsed.SourceType);
        Assert.Equal("windows", parsed.SourceProduct);
    }

    [Fact]
    public void Parse4624_NetworkLogonInsideWindow_RemainsLoginSuccess()
    {
        var json = SiemFixtureHelper.ReadFixture("windows_4624_success_logon.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows", Host = "dc01" },
            Raw = doc.RootElement.Clone()
        };

        var parsed = _parser.Parse(ctx);

        Assert.Equal("login_success", parsed.EventAction);
    }

    [Fact]
    public void Parse4624_RdpOutsideMaintenanceWindow_MapsPrivilegedOutsideWindow()
    {
        var json = SiemFixtureHelper.ReadFixture("windows_4624_privileged_rdp_outside_window.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows", Host = "bastion01" },
            Raw = doc.RootElement.Clone()
        };

        var parsed = _parser.Parse(ctx);

        Assert.Equal("privileged_login_outside_window", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("4624", parsed.EventCode);
        Assert.Equal("admin", parsed.ActorUser);
    }
}
