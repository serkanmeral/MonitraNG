using System.Collections.Concurrent;
using MongoDB.Driver;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class MongoSecEventParseRuleCatalogStore : ISecEventParseRuleCatalogStore
{
    public const string RulesCollection = "sec_event_parse_rules";
    public const string MetaCollection = "sec_event_parse_catalog";
    public const string CustomFieldsCollection = "sec_event_custom_fields";

    private readonly IMongoClient _mongo;
    private static readonly ConcurrentDictionary<string, byte> Indexed = new(StringComparer.OrdinalIgnoreCase);

    public MongoSecEventParseRuleCatalogStore(IMongoClient mongo)
    {
        _mongo = mongo;
    }

    public async Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default)
    {
        if (!Indexed.TryAdd(databaseName, 0))
            return;

        try
        {
            var rules = Rules(databaseName);
            await rules.Indexes.CreateOneAsync(
                new CreateIndexModel<SecEventParseRuleDocument>(
                    Builders<SecEventParseRuleDocument>.IndexKeys.Ascending(x => x.RuleId),
                    new CreateIndexOptions { Name = "ux_rule_id", Unique = true }),
                cancellationToken: ct);
            await rules.Indexes.CreateOneAsync(
                new CreateIndexModel<SecEventParseRuleDocument>(
                    Builders<SecEventParseRuleDocument>.IndexKeys
                        .Descending(x => x.Priority)
                        .Ascending(x => x.RuleId),
                    new CreateIndexOptions { Name = "ix_priority_rule_id" }),
                cancellationToken: ct);
        }
        catch
        {
            Indexed.TryRemove(databaseName, out _);
            throw;
        }
    }

    public async Task<IReadOnlyList<SecEventParseRuleDocument>> ListAsync(
        string databaseName,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await Rules(databaseName)
            .Find(FilterDefinition<SecEventParseRuleDocument>.Empty)
            .ToListAsync(ct);
    }

    public async Task<SecEventParseRuleDocument?> GetByRuleIdAsync(
        string databaseName,
        string ruleId,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var key = NormalizeRuleId(ruleId);
        return await Rules(databaseName)
            .Find(x => x.RuleId == key)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(
        string databaseName,
        SecEventParseRuleDocument doc,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        doc.RuleId = NormalizeRuleId(doc.RuleId);
        var filter = Builders<SecEventParseRuleDocument>.Filter.Eq(x => x.RuleId, doc.RuleId);
        await Rules(databaseName).ReplaceOneAsync(
            filter,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<bool> DeleteByRuleIdAsync(
        string databaseName,
        string ruleId,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var result = await Rules(databaseName)
            .DeleteOneAsync(x => x.RuleId == NormalizeRuleId(ruleId), ct);
        return result.DeletedCount > 0;
    }

    public async Task<SecEventParseCatalogMetaDocument?> GetMetaAsync(
        string databaseName,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await Meta(databaseName)
            .Find(x => x.Id == SecEventParseCatalogMetaDocument.SingletonId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveMetaAsync(
        string databaseName,
        SecEventParseCatalogMetaDocument meta,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        meta.Id = SecEventParseCatalogMetaDocument.SingletonId;
        await Meta(databaseName).ReplaceOneAsync(
            x => x.Id == SecEventParseCatalogMetaDocument.SingletonId,
            meta,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<long> CountAsync(string databaseName, CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await Rules(databaseName).CountDocumentsAsync(
            FilterDefinition<SecEventParseRuleDocument>.Empty,
            cancellationToken: ct);
    }

    private IMongoCollection<SecEventParseRuleDocument> Rules(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<SecEventParseRuleDocument>(RulesCollection);

    private IMongoCollection<SecEventParseCatalogMetaDocument> Meta(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<SecEventParseCatalogMetaDocument>(MetaCollection);

    private IMongoCollection<SecEventCustomFieldDocument> CustomFields(string databaseName) =>
        _mongo.GetDatabase(databaseName).GetCollection<SecEventCustomFieldDocument>(CustomFieldsCollection);

    public async Task<IReadOnlyList<SecEventCustomFieldDocument>> ListCustomFieldsAsync(
        string databaseName,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        return await CustomFields(databaseName)
            .Find(FilterDefinition<SecEventCustomFieldDocument>.Empty)
            .SortBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task UpsertCustomFieldAsync(
        string databaseName,
        SecEventCustomFieldDocument doc,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        doc.Name = (doc.Name ?? string.Empty).Trim().ToLowerInvariant();
        var filter = Builders<SecEventCustomFieldDocument>.Filter.Eq(x => x.Name, doc.Name);
        await CustomFields(databaseName).ReplaceOneAsync(
            filter,
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<bool> DeleteCustomFieldAsync(
        string databaseName,
        string name,
        CancellationToken ct = default)
    {
        await EnsureIndexesAsync(databaseName, ct);
        var key = (name ?? string.Empty).Trim().ToLowerInvariant();
        var result = await CustomFields(databaseName)
            .DeleteOneAsync(x => x.Name == key, ct);
        return result.DeletedCount > 0;
    }

    private static string NormalizeRuleId(string ruleId) =>
        (ruleId ?? string.Empty).Trim().ToLowerInvariant();
}
