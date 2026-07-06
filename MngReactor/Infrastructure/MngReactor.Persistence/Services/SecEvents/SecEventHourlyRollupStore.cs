using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Driver;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

/// <summary>
/// Pre-aggregated hourly buckets for dashboard-summary (Faz 2.2).
/// Window key uses <see cref="SecEventDashboardAggregator.DashboardTimeField"/> (ingestedAt).
/// </summary>
internal sealed class SecEventHourlyRollupStore
{
    internal const string CollectionName = "sec_events_hourly_rollup";

    private static readonly ConcurrentDictionary<string, byte> IndexEnsuredDomains = new(StringComparer.OrdinalIgnoreCase);

    private readonly IMongoClient _mongoClient;

    public SecEventHourlyRollupStore(IMongoClient mongoClient) => _mongoClient = mongoClient;

    public async Task IncrementFromDocumentsAsync(
        string domain,
        IReadOnlyList<SecEventDocument> docs,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain) || docs.Count == 0)
            return;

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);
        await EnsureIndexesOnceAsync(database, databaseName, cancellationToken);

        var increments = new Dictionary<DateTime, RollupDelta>(docs.Count);
        foreach (var doc in docs)
        {
            var action = doc.Event.Action;
            if (string.IsNullOrWhiteSpace(action)
                || string.Equals(action, SecEventUnknownFilter.UnknownAction, StringComparison.OrdinalIgnoreCase))
                continue;

            var hour = TruncateHourUtc(doc.IngestedAt);
            if (!increments.TryGetValue(hour, out var delta))
            {
                delta = new RollupDelta();
                increments[hour] = delta;
            }

            delta.EventsTotal++;
            delta.IncrementAction(action);
            if (doc.BaselineNewFlowPair)
                delta.NewFlowCount++;
        }

        if (increments.Count == 0)
            return;

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var models = new List<WriteModel<BsonDocument>>(increments.Count);
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        foreach (var (hourStart, delta) in increments)
        {
            var id = BuildDocumentId(normalizedDomain, hourStart);
            var incDoc = new BsonDocument { { "eventsTotal", delta.EventsTotal } };
            if (delta.NewFlowCount > 0)
                incDoc["newFlowCount"] = delta.NewFlowCount;

            foreach (var (action, count) in delta.ActionCounts)
                incDoc[$"byAction.{action}"] = count;

            var update = new BsonDocument("$inc", incDoc)
                .Add("$set", new BsonDocument("updatedAt", new BsonDateTime(now)))
                .Add("$setOnInsert", new BsonDocument
                {
                    { "domain", normalizedDomain },
                    { "hourStart", new BsonDateTime(hourStart) },
                });

            models.Add(new UpdateOneModel<BsonDocument>(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                update)
            {
                IsUpsert = true,
            });
        }

        await collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
    }

    public async Task<SecEventDashboardSummary?> TryBuildSummaryAsync(
        string domain,
        int rangeHours,
        bool excludeUnknown,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return null;

        var (from, to, hourStarts) = SecEventDashboardAggregator.BuildWindow(rangeHours);
        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var collection = _mongoClient.GetDatabase(databaseName).GetCollection<BsonDocument>(CollectionName);

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("domain", domain.Trim().ToLowerInvariant()),
            Builders<BsonDocument>.Filter.Gte("hourStart", new BsonDateTime(from)),
            Builders<BsonDocument>.Filter.Lte("hourStart", new BsonDateTime(to)));

        var rows = await collection.Find(filter).ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return null;

        var rowByHour = new Dictionary<DateTime, BsonDocument>();
        foreach (var row in rows)
        {
            if (!row.TryGetValue("hourStart", out var hourVal) || !hourVal.IsValidDateTime)
                continue;
            rowByHour[TruncateHourUtc(hourVal.ToUniversalTime())] = row;
        }

        long eventsTotal = 0;
        var byAction = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long newFlowTotal = 0;

        foreach (var row in rows)
        {
            eventsTotal += ReadLong(row, "eventsTotal");
            newFlowTotal += ReadLong(row, "newFlowCount");
            MergeActionCounts(row, byAction, excludeUnknown);
        }

        if (newFlowTotal > 0)
            byAction[SecEventFlowBaselineRules.NewFlowAction] = newFlowTotal;

        var hourly = hourStarts
            .Select(start =>
            {
                var key = TruncateHourUtc(start);
                var count = rowByHour.TryGetValue(key, out var row)
                    ? ReadLong(row, "eventsTotal")
                    : 0L;
                return new SecEventHourlyBucket { HourStart = start, Count = count };
            })
            .ToList();

        return new SecEventDashboardSummary
        {
            Range = $"{rangeHours}h",
            From = from,
            To = to,
            EventsTotal = eventsTotal,
            ByAction = byAction,
            Hourly = hourly,
        };
    }

    private static void MergeActionCounts(
        BsonDocument row,
        Dictionary<string, long> byAction,
        bool excludeUnknown)
    {
        if (!row.TryGetValue("byAction", out var actionsVal) || !actionsVal.IsBsonDocument)
            return;

        foreach (var element in actionsVal.AsBsonDocument.Elements)
        {
            if (excludeUnknown
                && string.Equals(element.Name, SecEventUnknownFilter.UnknownAction, StringComparison.OrdinalIgnoreCase))
                continue;

            var count = element.Value.ToInt64();
            if (count <= 0)
                continue;

            byAction.TryGetValue(element.Name, out var existing);
            byAction[element.Name] = existing + count;
        }
    }

    private static long ReadLong(BsonDocument doc, string field) =>
        doc.TryGetValue(field, out var value) ? value.ToInt64() : 0L;

    private static string BuildDocumentId(string domain, DateTime hourStartUtc) =>
        $"{domain}|{hourStartUtc:O}";

    private static DateTime TruncateHourUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }

    private async Task EnsureIndexesOnceAsync(
        IMongoDatabase database,
        string databaseName,
        CancellationToken cancellationToken)
    {
        if (!IndexEnsuredDomains.TryAdd(databaseName, 0))
            return;

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var models = new List<CreateIndexModel<BsonDocument>>
        {
            new(
                Builders<BsonDocument>.IndexKeys
                    .Ascending("domain")
                    .Ascending("hourStart"),
                new CreateIndexOptions { Name = "idx_domain_hourStart" }),
        };

        try
        {
            await collection.Indexes.CreateManyAsync(models, cancellationToken);
        }
        catch (MongoCommandException ex) when (IsIndexConflict(ex))
        {
            // already exists
        }
    }

    private static bool IsIndexConflict(MongoCommandException ex) =>
        ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict" || ex.Code == 85;

    private sealed class RollupDelta
    {
        public long EventsTotal { get; set; }
        public long NewFlowCount { get; set; }
        public Dictionary<string, long> ActionCounts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void IncrementAction(string action)
        {
            ActionCounts.TryGetValue(action, out var count);
            ActionCounts[action] = count + 1;
        }
    }

    internal static BsonDocument BuildRollupUpsertDocument(string domain, DateTime hourStartUtc, BsonDocument aggregatedRow)
    {
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        var hour = hourStartUtc.Kind == DateTimeKind.Utc ? hourStartUtc : hourStartUtc.ToUniversalTime();
        hour = new DateTime(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, DateTimeKind.Utc);

        var doc = new BsonDocument
        {
            ["_id"] = BuildDocumentId(normalizedDomain, hour),
            ["domain"] = normalizedDomain,
            ["hourStart"] = new BsonDateTime(hour),
            ["eventsTotal"] = aggregatedRow.GetValue("eventsTotal", 0).ToInt64(),
            ["newFlowCount"] = aggregatedRow.GetValue("newFlowCount", 0).ToInt64(),
            ["byAction"] = aggregatedRow.GetValue("byAction", new BsonDocument()).AsBsonDocument,
            ["updatedAt"] = new BsonDateTime(DateTime.UtcNow),
        };
        return doc;
    }
}
