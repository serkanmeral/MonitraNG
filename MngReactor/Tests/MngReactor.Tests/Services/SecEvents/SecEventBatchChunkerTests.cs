using System.Text.Json;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

internal static class SecEventTestData
{
    public static SecEventRawContext FirewallDenyContext() =>
        new()
        {
            ReceivedAt = DateTime.Parse("2026-06-03T14:00:01Z").ToUniversalTime(),
            Source = new SecEventSourceInfo
            {
                Type = "firewall",
                Product = "generic-syslog",
                Host = "fw01"
            },
            Raw = JsonSerializer.SerializeToElement(SiemFixtureHelper.ReadFixture("firewall_deny.syslog.txt"))
        };
}

public sealed class SecEventBatchChunkerTests
{
    [Fact]
    public void Chunk_SplitsByMongoBulkSize()
    {
        var items = Enumerable.Range(1, 2500).ToList();
        var chunks = SecEventBatchChunker.Chunk(items, SecEventIngestLimits.MongoBulkChunkSize).ToList();

        Assert.Equal(3, chunks.Count);
        Assert.Equal(1000, chunks[0].Count);
        Assert.Equal(1000, chunks[1].Count);
        Assert.Equal(500, chunks[2].Count);
    }
}
