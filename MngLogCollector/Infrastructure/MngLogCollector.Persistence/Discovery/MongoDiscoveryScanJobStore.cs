using System.Collections.Concurrent;
using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Persistence.Discovery;

public sealed class MongoDiscoveryScanJobStore : IDiscoveryScanJobStore
{
    public const string CollectionName = "discovery_scan_jobs";

    private readonly IMongoClient _mongo;
    private static readonly ConcurrentDictionary<string, byte> Indexed = new(StringComparer.OrdinalIgnoreCase);

    public MongoDiscoveryScanJobStore(IMongoClient mongo)
    {
        _mongo = mongo;
    }

    public async Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default)
    {
        if (!Indexed.TryAdd(databaseName, 0))
            return;

        try
        {
            var col = Jobs(databaseName);
            await col.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<DiscoveryScanJob>(
                    Builders<DiscoveryScanJob>.IndexKeys
                        .Ascending(x => x.DomainId)
                        .Ascending(x => x.Status),
                    new CreateIndexOptions { Name = "ix_domain_status" }),
                new CreateIndexModel<DiscoveryScanJob>(
                    Builders<DiscoveryScanJob>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "ix_createdAt" })
            ], ct);
        }
        catch
        {
            Indexed.TryRemove(databaseName, out _);
            throw;
        }
    }

    public Task InsertAsync(string databaseName, DiscoveryScanJob job, CancellationToken ct = default) =>
        Jobs(databaseName).InsertOneAsync(job, cancellationToken: ct);

    public async Task<DiscoveryScanJob?> GetAsync(
        string databaseName,
        string runId,
        CancellationToken ct = default) =>
        await Jobs(databaseName)
            .Find(Builders<DiscoveryScanJob>.Filter.Eq(x => x.Id, runId))
            .FirstOrDefaultAsync(ct);

    public Task UpdateAsync(string databaseName, DiscoveryScanJob job, CancellationToken ct = default) =>
        Jobs(databaseName).ReplaceOneAsync(
            Builders<DiscoveryScanJob>.Filter.Eq(x => x.Id, job.Id),
            job,
            new ReplaceOptions { IsUpsert = false },
            ct);

    public async Task<DiscoveryScanJob?> FindActiveAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default)
    {
        var filter = Builders<DiscoveryScanJob>.Filter.And(
            Builders<DiscoveryScanJob>.Filter.Eq(x => x.DomainId, domainId),
            Builders<DiscoveryScanJob>.Filter.In(x => x.Status, ["queued", "running"]));

        return await Jobs(databaseName)
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    private IMongoCollection<DiscoveryScanJob> Jobs(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<DiscoveryScanJob>(CollectionName);
}
