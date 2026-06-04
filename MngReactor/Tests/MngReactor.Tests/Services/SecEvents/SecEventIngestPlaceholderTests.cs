using System.Text.Json;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventIngestPlaceholderTests
{
    private static SecEventIngestItem SampleItem => new()
    {
        ReceivedAt = DateTime.UtcNow,
        Source = new SecEventIngestSource { Type = "firewall", Product = "generic-syslog", Host = "fw01" },
        Raw = JsonSerializer.SerializeToElement("DENY test")
    };

    [Fact]
    public async Task Placeholder_ReturnsImplementationPending()
    {
        var sut = new SecEventIngestProcessingPlaceholder();
        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = [SampleItem] },
            "odak");

        Assert.True(response.ImplementationPending);
        Assert.Equal(0, response.Accepted);
        Assert.Equal(1, response.Rejected);
    }

    [Fact]
    public async Task Placeholder_RejectsOversizedBatch()
    {
        var sut = new SecEventIngestProcessingPlaceholder();
        var items = Enumerable.Repeat(SampleItem, SecEventIngestLimits.MaxItemsPerRequest + 1).ToList();
        var response = await sut.ProcessAsync(
            new SecEventIngestRequest { Items = items },
            "odak");

        Assert.True(response.ImplementationPending);
        Assert.Equal(0, response.Accepted);
        Assert.Equal(items.Count, response.Rejected);
        Assert.Contains("max items", response.Message!, StringComparison.OrdinalIgnoreCase);
    }
}
