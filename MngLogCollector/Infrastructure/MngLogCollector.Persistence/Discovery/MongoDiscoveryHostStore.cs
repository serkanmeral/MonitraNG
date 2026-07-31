using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Persistence.Discovery;

public sealed class MongoDiscoveryHostStore : IDiscoveryHostStore
{
    public const string HostsCollection = "discovery_hosts";
    public const string SyncStateCollection = "discovery_sync_state";

    private readonly IMongoClient _mongo;
    private static readonly ConcurrentDictionary<string, byte> Indexed = new(StringComparer.OrdinalIgnoreCase);

    public MongoDiscoveryHostStore(IMongoClient mongo)
    {
        _mongo = mongo;
    }

    public async Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default)
    {
        if (!Indexed.TryAdd(databaseName, 0))
            return;

        try
        {
            var hosts = Hosts(databaseName);
            var models = new[]
            {
                new CreateIndexModel<DiscoveryHost>(
                    Builders<DiscoveryHost>.IndexKeys
                        .Ascending(x => x.DomainId)
                        .Ascending(x => x.ObjectGuid),
                    new CreateIndexOptions { Name = "ux_domain_objectGuid", Unique = true }),
                new CreateIndexModel<DiscoveryHost>(
                    Builders<DiscoveryHost>.IndexKeys
                        .Ascending(x => x.DomainId)
                        .Ascending(x => x.SamAccountName),
                    new CreateIndexOptions { Name = "ix_domain_sam", Unique = false }),
                new CreateIndexModel<DiscoveryHost>(
                    Builders<DiscoveryHost>.IndexKeys.Ascending(x => x.Sources),
                    new CreateIndexOptions { Name = "ix_sources" })
            };
            await hosts.Indexes.CreateManyAsync(models, ct);
        }
        catch
        {
            Indexed.TryRemove(databaseName, out _);
            throw;
        }
    }

    public async Task UpsertManyAsync(
        string databaseName,
        IReadOnlyList<DiscoveryHost> hosts,
        CancellationToken ct = default)
    {
        if (hosts.Count == 0)
            return;

        var collection = Hosts(databaseName);
        var writes = new List<WriteModel<DiscoveryHost>>(hosts.Count);

        foreach (var host in hosts)
        {
            if (string.IsNullOrWhiteSpace(host.ObjectGuid))
                host.ObjectGuid = $"ad:{host.SamAccountName}";

            var filter = Builders<DiscoveryHost>.Filter.And(
                Builders<DiscoveryHost>.Filter.Eq(x => x.DomainId, host.DomainId),
                Builders<DiscoveryHost>.Filter.Eq(x => x.ObjectGuid, host.ObjectGuid));

            var update = Builders<DiscoveryHost>.Update
                .SetOnInsert(x => x.CreatedAt, host.CreatedAt)
                .Set(x => x.DomainId, host.DomainId)
                .Set(x => x.ObjectGuid, host.ObjectGuid)
                .Set(x => x.SamAccountName, host.SamAccountName)
                .Set(x => x.DnsHostName, host.DnsHostName)
                .Set(x => x.DisplayName, host.DisplayName)
                .Set(x => x.OperatingSystem, host.OperatingSystem)
                .Set(x => x.OperatingSystemVersion, host.OperatingSystemVersion)
                .Set(x => x.DistinguishedName, host.DistinguishedName)
                .Set(x => x.LastSeenFromAd, host.LastSeenFromAd)
                .Set(x => x.AdEnabled, host.AdEnabled)
                .Set(x => x.UpdatedAt, host.UpdatedAt)
                .Set(x => x.LastSyncRunId, host.LastSyncRunId)
                .AddToSetEach(x => x.Sources, host.Sources);

            writes.Add(new UpdateOneModel<DiscoveryHost>(filter, update) { IsUpsert = true });
        }

        await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }

    public async Task<(IReadOnlyList<DiscoveryHost> Items, long Total)> ListAsync(
        string databaseName,
        string domainId,
        string? query,
        string? source,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var filter = BuildFilter(domainId, query, source);
        var collection = Hosts(databaseName);
        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await collection.Find(filter)
            .SortBy(x => x.DisplayName)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<long> CountAsync(string databaseName, string domainId, CancellationToken ct = default) =>
        Hosts(databaseName).CountDocumentsAsync(
            Builders<DiscoveryHost>.Filter.Eq(x => x.DomainId, domainId),
            cancellationToken: ct);

    public async Task<Dictionary<string, int>> CountBySourceAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default)
    {
        var collection = Hosts(databaseName);
        var filter = Builders<DiscoveryHost>.Filter.Eq(x => x.DomainId, domainId);
        var pipeline = collection.Aggregate()
            .Match(filter)
            .Unwind(x => x.Sources)
            .Group(new BsonDocument
            {
                { "_id", "$sources" },
                { "count", new BsonDocument("$sum", 1) }
            });

        var docs = await pipeline.ToListAsync(ct);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in docs)
        {
            var key = doc.GetValue("_id", "").ToString() ?? "";
            if (string.IsNullOrWhiteSpace(key))
                continue;
            result[key] = doc.GetValue("count", 0).ToInt32();
        }
        return result;
    }

    public Task SaveSyncStateAsync(
        string databaseName,
        DiscoverySyncState state,
        CancellationToken ct = default)
    {
        var collection = SyncState(databaseName);
        var filter = Builders<DiscoverySyncState>.Filter.Eq(x => x.Id, state.Id);
        return collection.ReplaceOneAsync(filter, state, new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task<DiscoverySyncState?> GetSyncStateAsync(
        string databaseName,
        string sourceId = "ad",
        CancellationToken ct = default) =>
        await SyncState(databaseName)
            .Find(Builders<DiscoverySyncState>.Filter.Eq(x => x.Id, sourceId))
            .FirstOrDefaultAsync(ct);

    private IMongoCollection<DiscoveryHost> Hosts(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<DiscoveryHost>(HostsCollection);

    private IMongoCollection<DiscoverySyncState> SyncState(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<DiscoverySyncState>(SyncStateCollection);

    private static FilterDefinition<DiscoveryHost> BuildFilter(string domainId, string? query, string? source)
    {
        var filters = new List<FilterDefinition<DiscoveryHost>>
        {
            Builders<DiscoveryHost>.Filter.Eq(x => x.DomainId, domainId)
        };

        if (!string.IsNullOrWhiteSpace(source))
            filters.Add(Builders<DiscoveryHost>.Filter.AnyEq(x => x.Sources, source.Trim()));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filters.Add(Builders<DiscoveryHost>.Filter.Or(
                Builders<DiscoveryHost>.Filter.Regex(x => x.DisplayName, new BsonRegularExpression(q, "i")),
                Builders<DiscoveryHost>.Filter.Regex(x => x.DnsHostName, new BsonRegularExpression(q, "i")),
                Builders<DiscoveryHost>.Filter.Regex(x => x.SamAccountName, new BsonRegularExpression(q, "i"))));
        }

        return Builders<DiscoveryHost>.Filter.And(filters);
    }
}
