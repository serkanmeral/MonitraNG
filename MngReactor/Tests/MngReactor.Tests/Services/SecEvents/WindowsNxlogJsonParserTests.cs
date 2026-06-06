using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class WindowsNxlogJsonParserTests
{
    private readonly WindowsNxlogJsonParser _parser = SecEventParserTestFactory.CreateNxlogParser();

    [Fact]
    public void CanParse_Nxlog4625String_ReturnsTrue()
    {
        var ctx = CreateContext(SiemFixtureHelper.ReadFixture("nxlog_terminal_4625.json.txt"));
        Assert.True(_parser.CanParse(ctx));
    }

    [Fact]
    public void Parse_Nxlog4625_MapsLoginFailed()
    {
        var ctx = CreateContext(SiemFixtureHelper.ReadFixture("nxlog_terminal_4625.json.txt"));
        var parsed = _parser.Parse(ctx);

        Assert.Equal(WindowsNxlogJsonParser.ParserIdValue, parsed.ParserId);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("failure", parsed.EventOutcome);
        Assert.Equal("4625", parsed.EventCode);
        Assert.Equal("probe_fail_user", parsed.ActorUser);
        Assert.Equal("192.168.20.99", parsed.NetworkSrcIp);
        Assert.Equal("TERMINAL.odak.local", parsed.SourceHost);
        Assert.Equal(WindowsNxlogJsonParser.ProductValue, parsed.SourceProduct);
    }

    [Fact]
    public void CanParse_SysmonProcessCreate_ReturnsFalse()
    {
        var ctx = CreateContext(SiemFixtureHelper.ReadFixture("nxlog_terminal_sysmon_process.json.txt"));
        Assert.False(_parser.CanParse(ctx));
    }

    [Fact]
    public void Registry_RoutesNxlogStringToNxlogParser()
    {
        var registry = SecEventParserTestFactory.CreateRegistry();
        var ctx = CreateContext(
            SiemFixtureHelper.ReadFixture("nxlog_terminal_4625.json.txt"),
            type: "ad",
            product: WindowsNxlogJsonParser.ProductValue);

        var parser = registry.Resolve(ctx);
        Assert.Equal(WindowsNxlogJsonParser.ParserIdValue, parser.ParserId);
    }

    private static SecEventRawContext CreateContext(
        string rawText,
        string type = "ad",
        string product = "windows-nxlog",
        string host = "TERMINAL.odak.local")
    {
        return new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = type, Product = product, Host = host },
            Raw = JsonSerializer.SerializeToElement(rawText)
        };
    }
}
