using MongoDB.Bson;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

internal static class SecEventDashboardAggregator
{
    public static (DateTime From, DateTime To, IReadOnlyList<DateTime> HourStarts) BuildWindow(int rangeHours)
    {
        var hours = Math.Clamp(rangeHours, 1, 168);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-hours);
        var hourStarts = new List<DateTime>(hours);
        for (var idx = 0; idx < hours; idx++)
        {
            var bucketEnd = to.AddHours(-(hours - 1 - idx));
            var bucketStart = bucketEnd.AddHours(-1);
            hourStarts.Add(DateTime.SpecifyKind(bucketStart, DateTimeKind.Utc));
        }

        return (from, to, hourStarts);
    }

    /// <summary>Dashboard window uses ingestedAt so panel reflects arrivals even when @timestamp is ahead (NxLog local-time skew).</summary>
    public const string DashboardTimeField = "ingestedAt";

    public static BsonDocument[] BuildPipeline(DateTime from, DateTime to, bool excludeUnknown)
    {
        var match = new BsonDocument(DashboardTimeField, new BsonDocument
        {
            { "$gte", new BsonDateTime(from) },
            { "$lte", new BsonDateTime(to) },
        });

        if (excludeUnknown)
        {
            match = new BsonDocument("$and", new BsonArray
            {
                match,
                new BsonDocument("event.action", new BsonDocument("$ne", SecEventUnknownFilter.UnknownAction)),
            });
        }

        return
        [
            new BsonDocument("$match", match),
            new BsonDocument("$project", new BsonDocument
            {
                { DashboardTimeField, 1 },
                { "event.action", 1 },
                { "baseline.newFlowPair", 1 },
            }),
            new BsonDocument("$facet", new BsonDocument
            {
                {
                    "total", new BsonArray
                    {
                        new BsonDocument("$count", "n")
                    }
                },
                {
                    "byAction", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", "$event.action" },
                            { "count", new BsonDocument("$sum", 1) },
                        }),
                    }
                },
                {
                    "newFlow", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("baseline.newFlowPair", true)),
                        new BsonDocument("$count", "n"),
                    }
                },
                {
                    "hourly", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            {
                                "_id", new BsonDocument("$dateTrunc", new BsonDocument
                                {
                                    { "date", $"${DashboardTimeField}" },
                                    { "unit", "hour" },
                                    { "timezone", "UTC" },
                                })
                            },
                            { "count", new BsonDocument("$sum", 1) },
                        }),
                        new BsonDocument("$sort", new BsonDocument("_id", 1)),
                    }
                },
            }),
        ];
    }

    public static SecEventDashboardSummary ParseResult(
        BsonDocument facetRoot,
        int rangeHours,
        DateTime from,
        DateTime to,
        IReadOnlyList<DateTime> hourStarts)
    {
        var total = ReadCount(facetRoot, "total");
        var byAction = ReadActionCounts(facetRoot);
        var newFlow = ReadCount(facetRoot, "newFlow");
        if (newFlow > 0)
            byAction[SecEventFlowBaselineRules.NewFlowAction] = newFlow;

        var hourlyMap = ReadHourlyCounts(facetRoot);
        var hourly = hourStarts
            .Select(start =>
            {
                var key = TruncateHourUtc(start);
                hourlyMap.TryGetValue(key, out var count);
                return new SecEventHourlyBucket { HourStart = start, Count = count };
            })
            .ToList();

        return new SecEventDashboardSummary
        {
            Range = $"{rangeHours}h",
            From = from,
            To = to,
            EventsTotal = total,
            ByAction = byAction,
            Hourly = hourly,
        };
    }

    private static long ReadCount(BsonDocument facetRoot, string branch)
    {
        if (!facetRoot.TryGetValue(branch, out var arr) || !arr.IsBsonArray || arr.AsBsonArray.Count == 0)
            return 0;
        var first = arr.AsBsonArray[0].AsBsonDocument;
        return first.TryGetValue("n", out var n) ? n.ToInt64() : 0;
    }

    private static Dictionary<string, long> ReadActionCounts(BsonDocument facetRoot)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!facetRoot.TryGetValue("byAction", out var arr) || !arr.IsBsonArray)
            return result;

        foreach (var item in arr.AsBsonArray)
        {
            if (!item.IsBsonDocument)
                continue;
            var doc = item.AsBsonDocument;
            var id = doc.GetValue("_id", BsonNull.Value);
            if (id.IsBsonNull || id.IsString && string.IsNullOrWhiteSpace(id.AsString))
                continue;
            var key = id.IsString ? id.AsString : id.ToString() ?? "unknown";
            var count = doc.GetValue("count", 0).ToInt64();
            if (count > 0)
                result[key] = count;
        }

        return result;
    }

    private static Dictionary<DateTime, long> ReadHourlyCounts(BsonDocument facetRoot)
    {
        var result = new Dictionary<DateTime, long>();
        if (!facetRoot.TryGetValue("hourly", out var arr) || !arr.IsBsonArray)
            return result;

        foreach (var item in arr.AsBsonArray)
        {
            if (!item.IsBsonDocument)
                continue;
            var doc = item.AsBsonDocument;
            if (!doc.TryGetValue("_id", out var id) || !id.IsValidDateTime)
                continue;
            var hour = TruncateHourUtc(id.ToUniversalTime());
            result[hour] = doc.GetValue("count", 0).ToInt64();
        }

        return result;
    }

    private static DateTime TruncateHourUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }
}
