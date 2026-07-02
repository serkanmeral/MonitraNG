using MongoDB.Bson;
using MngReactor.Persistence.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventDashboardAggregatorTests
{
    [Fact]
    public void ParseResult_MergesNewFlowAndFillsHourlyBuckets()
    {
        var from = new DateTime(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        var to = from.AddHours(24);
        var hourStarts = new[]
        {
            from,
            from.AddHours(1),
        };

        var facet = new BsonDocument
        {
            { "total", new BsonArray { new BsonDocument("n", 12) } },
            {
                "byAction", new BsonArray
                {
                    new BsonDocument { { "_id", "login_failed" }, { "count", 5 } },
                    new BsonDocument { { "_id", "denied_flow" }, { "count", 3 } },
                }
            },
            { "newFlow", new BsonArray { new BsonDocument("n", 2) } },
            {
                "hourly", new BsonArray
                {
                    new BsonDocument
                    {
                        { "_id", new BsonDateTime(from) },
                        { "count", 4 },
                    },
                }
            },
        };

        var summary = SecEventDashboardAggregator.ParseResult(facet, 24, from, to, hourStarts);

        Assert.Equal(12, summary.EventsTotal);
        Assert.Equal(5, summary.ByAction["login_failed"]);
        Assert.Equal(3, summary.ByAction["denied_flow"]);
        Assert.Equal(2, summary.ByAction["new_flow"]);
        Assert.Equal(4, summary.Hourly[0].Count);
        Assert.Equal(0, summary.Hourly[1].Count);
    }

    [Fact]
    public void BuildPipeline_UsesIngestedAtForWindow()
    {
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddHours(24);
        var pipeline = SecEventDashboardAggregator.BuildPipeline(from, to, excludeUnknown: true);
        var json = pipeline[0].ToJson();
        Assert.Contains(SecEventDashboardAggregator.DashboardTimeField, json);
    }

    [Fact]
    public void BuildWindow_ReturnsRequestedBucketCount()
    {
        var (_, _, hourStarts) = SecEventDashboardAggregator.BuildWindow(24);
        Assert.Equal(24, hourStarts.Count);
    }
}
