using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class WindowsSecurityExtendedParserTests
{
    private readonly WindowsSecurityExtendedParser _parser = new();
    private readonly WindowsSecurityParser _windowsParser = SecEventParserTestFactory.CreateWindowsParser();

    [Theory]
    [InlineData("windows_4720_account_created.json", "account_created", "4720", "administrator")]
    [InlineData("windows_4728_group_member_added.json", "group_member_added", "4728", "administrator")]
    [InlineData("windows_5136_directory_modified.json", "directory_object_modified", "5136", "administrator")]
    [InlineData("windows_4722_account_enabled.json", "account_enabled", "4722", "administrator")]
    [InlineData("windows_4726_account_deleted.json", "account_deleted", "4726", "administrator")]
    public void ParseExtendedEvent_MapsExpectedFields(
        string fixture,
        string expectedAction,
        string expectedCode,
        string expectedActor)
    {
        using var doc = JsonDocument.Parse(SiemFixtureHelper.ReadFixture(fixture));
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows", Host = "dc01" },
            Raw = doc.RootElement.Clone()
        };

        Assert.True(_parser.CanParse(ctx));
        Assert.False(_windowsParser.CanParse(ctx));

        var parsed = _parser.Parse(ctx);

        Assert.Equal(WindowsSecurityExtendedParser.ParserIdValue, parsed.ParserId);
        Assert.Equal(expectedAction, parsed.EventAction);
        Assert.Equal("success", parsed.EventOutcome);
        Assert.Equal(expectedCode, parsed.EventCode);
        Assert.Equal(expectedActor, parsed.ActorUser);
    }

    [Fact]
    public void CanParse_4625_ReturnsFalse()
    {
        using var doc = JsonDocument.Parse(SiemFixtureHelper.ReadFixture("windows_4625_failed_logon.json"));
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows" },
            Raw = doc.RootElement.Clone()
        };

        Assert.False(_parser.CanParse(ctx));
        Assert.True(_windowsParser.CanParse(ctx));
    }
}
