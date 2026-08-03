using MngReactor.Application.Contracts.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventParseRuleCatalogService
{
    /// <summary>Ensures meta (+ builtin seed when empty) exists for the tenant DB.</summary>
    Task EnsureCatalogReadyAsync(string domain, CancellationToken ct = default);

    Task<SecEventParseRuleManageListResponse> ListManagedAsync(string domain, CancellationToken ct = default);

    Task<SecEventParseRuleManageItemDto?> GetManagedAsync(string domain, string ruleId, CancellationToken ct = default);

    Task<SecEventParseRuleManageItemDto> CreateAsync(
        string domain,
        SecEventParseRuleUpsertRequest request,
        CancellationToken ct = default);

    Task<SecEventParseRuleManageItemDto> UpdateAsync(
        string domain,
        string ruleId,
        SecEventParseRuleUpsertRequest request,
        CancellationToken ct = default);

    Task DeleteAsync(string domain, string ruleId, CancellationToken ct = default);

    Task<SecEventParseRulePublishedResponse> PublishAsync(string domain, CancellationToken ct = default);

    Task<SecEventParseRulePublishedResponse> GetPublishedAsync(string domain, CancellationToken ct = default);

    Task<SecEventParseRulePreviewResponse> PreviewAsync(
        string domain,
        SecEventParseRulePreviewRequest request,
        CancellationToken ct = default);

    Task<SecEventTargetFieldCatalogResponse> GetTargetFieldsAsync(
        string domain,
        CancellationToken ct = default);

    Task<SecEventTargetFieldDefinition> UpsertCustomFieldAsync(
        string domain,
        SecEventCustomFieldUpsertRequest request,
        CancellationToken ct = default);

    Task DeleteCustomFieldAsync(string domain, string name, CancellationToken ct = default);
}
