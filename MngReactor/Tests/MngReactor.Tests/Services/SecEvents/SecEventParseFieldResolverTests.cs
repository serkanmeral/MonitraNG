using System.Text.Json;
using MngReactor.Application.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventParseFieldResolverTests
{
    [Fact]
    public void ReadEventData_resolves_agent_and_fields_shapes()
    {
        using var agent = JsonDocument.Parse("""
            {"eventId":21,"channel":"Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
             "eventData":{"User":"ODAK\\monitra","Address":"192.168.20.50"}}
            """);
        Assert.Equal("ODAK\\monitra", SecEventParseFieldResolver.ReadEventData(agent.RootElement, "User"));
        Assert.Equal(21, SecEventParseFieldResolver.ReadEventId(agent.RootElement));

        using var os = JsonDocument.Parse("""
            {"fields":{"eventId":65002,"channel":"Application",
             "eventData":{"Data_0":"failed to connect rabbitmq"},
             "eventDataText":"failed to connect rabbitmq"}}
            """);
        Assert.Equal(65002, SecEventParseFieldResolver.ReadEventId(os.RootElement));
        Assert.Equal("Application", SecEventParseFieldResolver.ReadChannel(os.RootElement));
        Assert.Equal("failed to connect rabbitmq", SecEventParseFieldResolver.ReadEventData(os.RootElement, "Data_0"));
        Assert.Equal("failed to connect rabbitmq", SecEventParseFieldResolver.ReadMessage(os.RootElement));
        Assert.Equal("text", SecEventParseFieldResolver.InferParseModeHint(
            SecEventParseFieldResolver.DiscoverEventDataKeys(os.RootElement)));
    }

    [Fact]
    public void MatchesSourceProduct_accepts_package_name_for_windows_rules()
    {
        Assert.True(SecEventParseFieldResolver.MatchesSourceProduct(
            ["windows"], "rdp-session", "windows-eventlog"));
        Assert.True(SecEventParseFieldResolver.MatchesSourceType(
            ["ad", "endpoint"], "windows-eventlog"));
        Assert.False(SecEventParseFieldResolver.MatchesSourceProduct(
            ["linux-journal"], "rdp-session", "windows-eventlog"));
    }

    [Fact]
    public void MatchesSourceProduct_accepts_agent_linux_journal()
    {
        Assert.True(SecEventParseFieldResolver.MatchesSourceProduct(
            ["linux-journal", "linux-syslog"], "mnglogs-agent", "linux-journal"));
        Assert.True(SecEventParseFieldResolver.MatchesSourceType(
            ["endpoint", "linux"], "linux-journal"));
    }
}
