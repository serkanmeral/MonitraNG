using MngLogCollector.Application.Contracts.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Abstractions.Policy;

public interface IDlpPolicyCatalogStore
{
    Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default);

    Task<DlpPolicyDocument?> GetAsync(string databaseName, string id, CancellationToken ct = default);

    Task UpsertAsync(string databaseName, DlpPolicyDocument doc, CancellationToken ct = default);

    Task<DlpCatalogMetaDocument> GetMetaAsync(string databaseName, CancellationToken ct = default);

    Task SaveMetaAsync(string databaseName, DlpCatalogMetaDocument meta, CancellationToken ct = default);
}

public interface IDlpPolicyCatalogService
{
    /// <summary>Published snapshot for agents. Never returns unpublished draft.</summary>
    Task<DlpPolicyResponse> GetPublishedAsync(CancellationToken ct = default);

    Task<DlpPolicyManageResponse> GetManageAsync(CancellationToken ct = default);

    Task<DlpPolicyManageResponse> UpsertDraftAsync(DlpPolicyUpsertRequest request, CancellationToken ct = default);

    Task<DlpPolicyResponse> PublishAsync(CancellationToken ct = default);
}
