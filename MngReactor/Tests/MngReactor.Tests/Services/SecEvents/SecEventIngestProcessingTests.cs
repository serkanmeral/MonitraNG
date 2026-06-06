using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Observations;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents.Parsers;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventIngestProcessingTests
{
    private static SecEventIngestProcessing CreateSut(
        Mock<ISecEventsRepository>? repoMock = null,
        Mock<ISecEventPublisher>? publisherMock = null,
        Mock<IObservationPublisher>? observationPublisherMock = null,
        SecEventsSettings? secEventsSettings = null)
    {
        repoMock ??= new Mock<ISecEventsRepository>();
        publisherMock ??= new Mock<ISecEventPublisher>();
        observationPublisherMock ??= new Mock<IObservationPublisher>();
        var baselineStoreMock = new Mock<ISecEventFlowBaselineStore>();
        baselineStoreMock
            .Setup(b => b.ApplyFlowPairAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string _, string _, CancellationToken _) =>
                new SecEventFlowBaselineApplyResult(false));

        repoMock
            .Setup(r => r.InsertManyAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<SecEventDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<SecEventDocument> docs, CancellationToken _) => docs.Count);

        publisherMock
            .Setup(p => p.PublishCreatedAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<SecEventCreatedMessage>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        observationPublisherMock
            .Setup(p => p.PublishSecEventAsync(
                It.IsAny<MngReactor.Application.Observations.SecEventObservationPayload>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new SecEventIngestProcessing(
            NullLogger<SecEventIngestProcessing>.Instance,
            SecEventParserTestFactory.CreateRegistry(),
            new UnknownSecEventFallback(),
            repoMock.Object,
            publisherMock.Object,
            observationPublisherMock.Object,
            baselineStoreMock.Object,
            Options.Create(new MngReactorSettings
            {
                SecEvents = secEventsSettings ?? new SecEventsSettings { DropUnknownEvents = false }
            }));
    }

    private static SecEventIngestItem FirewallItem =>
        new()
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventIngestSource
            {
                Type = "firewall",
                Product = "generic-syslog",
                Host = "fw01"
            },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("firewall_deny.syslog.txt"))
        };

    [Fact]
    public async Task ProcessAsync_FirewallFixture_ParsesInsertsAndPublishes()
    {
        var repo = new Mock<ISecEventsRepository>();
        var publisher = new Mock<ISecEventPublisher>();
        var sut = CreateSut(repo, publisher);

        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [FirewallItem] },
            "odak");

        Assert.Equal(1, response.Accepted);
        Assert.Equal(0, response.Rejected);
        Assert.Equal(1, response.Published);
        Assert.False(response.ImplementationPending);

        repo.Verify(r => r.InsertManyAsync(
            "odak",
            It.Is<IReadOnlyList<SecEventDocument>>(docs =>
                docs.Count == 1 && docs[0].Event.Action == "denied_flow"),
            It.IsAny<CancellationToken>()));

        publisher.Verify(p => p.PublishCreatedAsync(
            "odak",
            It.Is<IReadOnlyList<SecEventCreatedMessage>>(msgs =>
                msgs.Count == 1
                && msgs[0].EventAction == "denied_flow"
                && msgs[0].NetworkSrcIp == "203.0.113.5"),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ProcessAsync_FirewallFixture_PublishesSecEventObservation()
    {
        var observationPublisher = new Mock<IObservationPublisher>();
        var sut = CreateSut(observationPublisherMock: observationPublisher);

        await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [FirewallItem] },
            "odak");

        observationPublisher.Verify(o => o.PublishSecEventAsync(
            It.Is<MngReactor.Application.Observations.SecEventObservationPayload>(p =>
                p.Key == "denied_flow"
                && p.Kind == "event"
                && p.Dimensions.ContainsKey("srcIp")),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ProcessAsync_UnparseableFixture_UsesFallbackAndStillPersists()
    {
        var repo = new Mock<ISecEventsRepository>();
        var publisher = new Mock<ISecEventPublisher>();
        var sut = CreateSut(repo, publisher, secEventsSettings: new SecEventsSettings { DropUnknownEvents = false });

        var item = new SecEventIngestItem
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventIngestSource { Type = "unknown" },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("unparseable_01.txt"))
        };

        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [item] },
            "odak");

        Assert.Equal(1, response.Accepted);
        Assert.Equal(0, response.Skipped);
        Assert.Equal(1, response.Published);

        repo.Verify(r => r.InsertManyAsync(
            "odak",
            It.Is<IReadOnlyList<SecEventDocument>>(docs =>
                docs[0].Event.Action == "unknown"
                && docs[0].Parser.Id == UnknownSecEventFallback.ParserIdValue),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ProcessAsync_UnknownDroppedWhenConfigured_SkipsPersistAndObservation()
    {
        var repo = new Mock<ISecEventsRepository>();
        var observationPublisher = new Mock<IObservationPublisher>();
        var sut = CreateSut(
            repo,
            observationPublisherMock: observationPublisher,
            secEventsSettings: new SecEventsSettings { DropUnknownEvents = true });

        var item = new SecEventIngestItem
        {
            ReceivedAt = DateTime.UtcNow,
            Source = new SecEventIngestSource { Type = "unknown" },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("unparseable_01.txt"))
        };

        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [item] },
            "odak");

        Assert.Equal(0, response.Accepted);
        Assert.Equal(1, response.Skipped);
        Assert.Equal(0, response.Published);

        repo.Verify(
            r => r.InsertManyAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<SecEventDocument>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        observationPublisher.Verify(
            o => o.PublishSecEventAsync(
                It.IsAny<MngReactor.Application.Observations.SecEventObservationPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_PersistFullRawFalse_DoesNotStoreRawOnDocument()
    {
        var repo = new Mock<ISecEventsRepository>();
        repo
            .Setup(r => r.InsertManyAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<SecEventDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, IReadOnlyList<SecEventDocument> docs, CancellationToken _) => docs.Count);

        var sut = CreateSut(
            repo,
            secEventsSettings: new SecEventsSettings { DropUnknownEvents = false, PersistFullRaw = false });

        await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [FirewallItem] },
            "odak");

        repo.Verify(r => r.InsertManyAsync(
            "odak",
            It.Is<IReadOnlyList<SecEventDocument>>(docs =>
                docs.Count == 1
                && docs[0].Raw == string.Empty
                && !string.IsNullOrEmpty(docs[0].RawPreview)),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task ProcessAsync_RejectsOversizedBatch()
    {
        var sut = CreateSut();
        var items = Enumerable.Repeat(FirewallItem, SecEventIngestLimits.MaxItemsPerRequest + 1).ToList();

        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = items },
            "odak");

        Assert.Equal(0, response.Accepted);
        Assert.Equal(items.Count, response.Rejected);
        Assert.Equal(0, response.Published);
        Assert.Contains("max items", response.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_EmptyDomain_RejectsAllItems()
    {
        var sut = CreateSut();

        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [FirewallItem] },
            "  ");

        Assert.Equal(0, response.Accepted);
        Assert.Equal(1, response.Rejected);
        Assert.Contains("Domain required", response.Message!, StringComparison.OrdinalIgnoreCase);
    }
}
