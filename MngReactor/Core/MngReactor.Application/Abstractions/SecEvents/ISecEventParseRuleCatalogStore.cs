using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventParseRuleCatalogStore
{
    Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default);

    Task<IReadOnlyList<SecEventParseRuleDocument>> ListAsync(string databaseName, CancellationToken ct = default);

    Task<SecEventParseRuleDocument?> GetByRuleIdAsync(
        string databaseName,
        string ruleId,
        CancellationToken ct = default);

    Task UpsertAsync(string databaseName, SecEventParseRuleDocument doc, CancellationToken ct = default);

    Task<bool> DeleteByRuleIdAsync(string databaseName, string ruleId, CancellationToken ct = default);

    /// <summary>Returns null when the meta singleton has never been written.</summary>
    Task<SecEventParseCatalogMetaDocument?> GetMetaAsync(string databaseName, CancellationToken ct = default);

    Task SaveMetaAsync(string databaseName, SecEventParseCatalogMetaDocument meta, CancellationToken ct = default);

    Task<long> CountAsync(string databaseName, CancellationToken ct = default);

    Task<IReadOnlyList<SecEventCustomFieldDocument>> ListCustomFieldsAsync(
        string databaseName,
        CancellationToken ct = default);

    Task UpsertCustomFieldAsync(
        string databaseName,
        SecEventCustomFieldDocument doc,
        CancellationToken ct = default);

    Task<bool> DeleteCustomFieldAsync(
        string databaseName,
        string name,
        CancellationToken ct = default);
}
