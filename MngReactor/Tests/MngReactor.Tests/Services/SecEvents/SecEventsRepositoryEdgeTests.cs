using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngReactor.Application.Configuration;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventsRepositoryEdgeTests
{
    [Fact]
    public async Task InsertMany_EmptyDomain_ReturnsZeroWithoutMongoCall()
    {
        var client = new Mock<IMongoClient>(MockBehavior.Strict);
        var sut = new SecEventsRepository(
            client.Object,
            Options.Create(new MngReactorSettings()),
            NullLogger<SecEventsRepository>.Instance);

        var inserted = await sut.InsertManyAsync("  ", [SampleDoc()]);

        Assert.Equal(0, inserted);
        client.Verify(c => c.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()), Times.Never);
    }

    [Fact]
    public async Task InsertMany_EmptyDocs_ReturnsZeroWithoutMongoCall()
    {
        var client = new Mock<IMongoClient>(MockBehavior.Strict);
        var sut = new SecEventsRepository(
            client.Object,
            Options.Create(new MngReactorSettings()),
            NullLogger<SecEventsRepository>.Instance);

        var inserted = await sut.InsertManyAsync("odak", []);

        Assert.Equal(0, inserted);
        client.Verify(c => c.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()), Times.Never);
    }

    private static SecEventDocument SampleDoc() =>
        SecEventDocument.FromParsed(
            new ParsedSecEvent
            {
                Timestamp = DateTime.UtcNow,
                EventAction = "unknown",
                SourceType = "unknown",
                SourceProduct = "unknown",
                ParserId = UnknownSecEventFallback.ParserIdValue,
                Raw = "preview",
                RawPreview = "preview"
            },
            "odak",
            DateTime.UtcNow);
}
