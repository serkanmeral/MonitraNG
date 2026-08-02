using System.Collections.Concurrent;
using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Persistence.Policy;

public sealed class MongoEventLogPackageCatalogStore : IEventLogPackageCatalogStore
{
    public const string PackagesCollection = "eventlog_packages";
    public const string MetaCollection = "eventlog_catalog_meta";
    public const string AssignmentsCollection = "eventlog_host_assignments";

    private readonly IMongoClient _mongo;
    private static readonly ConcurrentDictionary<string, byte> Indexed = new(StringComparer.OrdinalIgnoreCase);

    public MongoEventLogPackageCatalogStore(IMongoClient mongo)
    {
        _mongo = mongo;
    }

    public async Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default)
    {
        if (!Indexed.TryAdd(databaseName, 0))
            return;

        try
        {
            var packages = Packages(databaseName);
            await packages.Indexes.CreateOneAsync(
                new CreateIndexModel<EventLogPackageDocument>(
                    Builders<EventLogPackageDocument>.IndexKeys.Ascending(x => x.Name),
                    new CreateIndexOptions { Name = "ux_name", Unique = true }),
                cancellationToken: ct);
        }
        catch
        {
            Indexed.TryRemove(databaseName, out _);
            throw;
        }
    }

    public async Task<IReadOnlyList<EventLogPackageDocument>> ListAsync(
        string databaseName,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var list = await Packages(databaseName)
            .Find(FilterDefinition<EventLogPackageDocument>.Empty)
            .SortBy(x => x.Name)
            .ToListAsync(ct);
        return list;
    }

    public async Task<EventLogPackageDocument?> GetByNameAsync(
        string databaseName,
        string name,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var key = NormalizeName(name);
        return await Packages(databaseName)
            .Find(x => x.Name == key)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(
        string databaseName,
        EventLogPackageDocument doc,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        doc.Name = NormalizeName(doc.Name);
        var filter = Builders<EventLogPackageDocument>.Filter.Eq(x => x.Name, doc.Name);
        await Packages(databaseName).ReplaceOneAsync(
            filter,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<bool> DeleteByNameAsync(
        string databaseName,
        string name,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var result = await Packages(databaseName).DeleteOneAsync(x => x.Name == NormalizeName(name), ct);
        return result.DeletedCount > 0;
    }

    public async Task<EventLogCatalogMetaDocument> GetMetaAsync(
        string databaseName,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var meta = await Meta(databaseName)
            .Find(x => x.Id == EventLogCatalogMetaDocument.SingletonId)
            .FirstOrDefaultAsync(ct);
        return meta ?? new EventLogCatalogMetaDocument();
    }

    public async Task SaveMetaAsync(
        string databaseName,
        EventLogCatalogMetaDocument meta,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        meta.Id = EventLogCatalogMetaDocument.SingletonId;
        await Meta(databaseName).ReplaceOneAsync(
            x => x.Id == EventLogCatalogMetaDocument.SingletonId,
            meta,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<long> CountAsync(string databaseName, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await Packages(databaseName).CountDocumentsAsync(
            FilterDefinition<EventLogPackageDocument>.Empty,
            cancellationToken: ct);
    }

    public async Task<EventLogHostAssignmentDocument?> GetAssignmentAsync(
        string databaseName,
        string hostKey,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var key = NormalizeHostKey(hostKey);
        if (string.IsNullOrEmpty(key))
            return null;
        return await Assignments(databaseName)
            .Find(x => x.HostKey == key)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAssignmentAsync(
        string databaseName,
        EventLogHostAssignmentDocument doc,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        doc.HostKey = NormalizeHostKey(doc.HostKey);
        if (string.IsNullOrEmpty(doc.HostKey))
            throw new ArgumentException("HostKey is required.");

        await Assignments(databaseName).ReplaceOneAsync(
            x => x.HostKey == doc.HostKey,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<bool> DeleteAssignmentAsync(
        string databaseName,
        string hostKey,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var key = NormalizeHostKey(hostKey);
        if (string.IsNullOrEmpty(key))
            return false;
        var result = await Assignments(databaseName).DeleteOneAsync(x => x.HostKey == key, ct);
        return result.DeletedCount > 0;
    }

    private IMongoCollection<EventLogPackageDocument> Packages(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<EventLogPackageDocument>(PackagesCollection);

    private IMongoCollection<EventLogCatalogMetaDocument> Meta(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<EventLogCatalogMetaDocument>(MetaCollection);

    private IMongoCollection<EventLogHostAssignmentDocument> Assignments(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<EventLogHostAssignmentDocument>(AssignmentsCollection);

    private static string NormalizeName(string name) => (name ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeHostKey(string host) =>
        (host ?? string.Empty).Trim().ToLowerInvariant().Split('.')[0];
}
