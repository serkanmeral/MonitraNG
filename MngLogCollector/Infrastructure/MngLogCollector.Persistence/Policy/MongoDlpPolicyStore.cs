using MongoDB.Driver;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Persistence.Policy;

public sealed class MongoDlpPolicyStore : IDlpPolicyCatalogStore
{
    public const string PoliciesCollection = "dlp_policies";
    public const string MetaCollection = "dlp_catalog_meta";

    private readonly IMongoClient _mongo;
    private static readonly HashSet<string> Indexed = new(StringComparer.OrdinalIgnoreCase);

    public MongoDlpPolicyStore(IMongoClient mongo)
    {
        _mongo = mongo;
    }

    public Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default)
    {
        lock (Indexed)
        {
            Indexed.Add(databaseName);
        }

        return Task.CompletedTask;
    }

    public async Task<DlpPolicyDocument?> GetAsync(string databaseName, string id, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await Policies(databaseName)
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(string databaseName, DlpPolicyDocument doc, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        await Policies(databaseName).ReplaceOneAsync(
            x => x.Id == doc.Id,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<DlpCatalogMetaDocument> GetMetaAsync(string databaseName, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var meta = await Meta(databaseName)
            .Find(x => x.Id == DlpCatalogMetaDocument.SingletonId)
            .FirstOrDefaultAsync(ct);
        return meta ?? new DlpCatalogMetaDocument();
    }

    public async Task SaveMetaAsync(string databaseName, DlpCatalogMetaDocument meta, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        meta.Id = DlpCatalogMetaDocument.SingletonId;
        await Meta(databaseName).ReplaceOneAsync(
            x => x.Id == DlpCatalogMetaDocument.SingletonId,
            meta,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    private IMongoCollection<DlpPolicyDocument> Policies(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<DlpPolicyDocument>(PoliciesCollection);

    private IMongoCollection<DlpCatalogMetaDocument> Meta(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<DlpCatalogMetaDocument>(MetaCollection);
}
