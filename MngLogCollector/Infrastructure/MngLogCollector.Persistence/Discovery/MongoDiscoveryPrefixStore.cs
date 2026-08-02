using System.Collections.Concurrent;
using MongoDB.Bson;
using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Persistence.Discovery;

public sealed class MongoDiscoveryPrefixStore : IDiscoveryPrefixStore
{
    public const string CollectionName = "discovery_prefix_tables";

    private readonly IMongoClient _mongo;
    private static readonly ConcurrentDictionary<string, byte> Indexed = new(StringComparer.OrdinalIgnoreCase);

    public MongoDiscoveryPrefixStore(IMongoClient mongo)
    {
        _mongo = mongo;
    }

    public async Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default)
    {
        if (!Indexed.TryAdd(databaseName, 0))
            return;

        try
        {
            await Collection(databaseName).Indexes.CreateOneAsync(
                new CreateIndexModel<DiscoveryPrefixTableDocument>(
                    Builders<DiscoveryPrefixTableDocument>.IndexKeys.Ascending(x => x.DomainId),
                    new CreateIndexOptions { Name = "ux_domainId", Unique = true }),
                cancellationToken: ct);
        }
        catch
        {
            Indexed.TryRemove(databaseName, out _);
            throw;
        }
    }

    public async Task<DiscoveryPrefixTableDocument?> GetAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await Collection(databaseName)
            .Find(Builders<DiscoveryPrefixTableDocument>.Filter.Eq(x => x.DomainId, domainId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(
        string databaseName,
        DiscoveryPrefixTableDocument document,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);

        var existing = await GetAsync(databaseName, document.DomainId, ct);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.Id))
            document.Id = existing.Id;
        else if (string.IsNullOrWhiteSpace(document.Id))
            document.Id = ObjectId.GenerateNewId().ToString();

        document.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<DiscoveryPrefixTableDocument>.Filter.Eq(x => x.DomainId, document.DomainId);
        await Collection(databaseName).ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    private IMongoCollection<DiscoveryPrefixTableDocument> Collection(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<DiscoveryPrefixTableDocument>(CollectionName);
}
