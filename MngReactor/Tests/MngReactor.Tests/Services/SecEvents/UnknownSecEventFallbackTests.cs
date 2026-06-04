using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class UnknownSecEventFallbackTests
{
    private readonly UnknownSecEventFallback _parser = new();

    [Fact]
    public void S2_3_UnparseableInput_ReturnsUnknownAction()
    {
        var rawLine = SiemFixtureHelper.ReadFixture("unparseable_01.txt");
        var ctx = new SecEventRawContext
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventSourceInfo { Type = "unknown", Product = "unknown" },
            Raw = JsonSerializer.SerializeToElement(rawLine)
        };

        Assert.True(_parser.CanParse(ctx));
        var parsed = _parser.Parse(ctx);

        Assert.Equal(UnknownSecEventFallback.ParserIdValue, parsed.ParserId);
        Assert.Equal("unknown", parsed.EventAction);
        Assert.Equal(rawLine, parsed.Raw);
        Assert.Equal(rawLine, parsed.RawPreview);
    }
}
