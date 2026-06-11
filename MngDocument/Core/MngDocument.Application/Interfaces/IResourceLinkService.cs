using MngDocument.Application.Contracts.ResourceLinks;

namespace MngDocument.Application.Interfaces;

/// <summary>
/// Document Intelligence kaynakları ile diğer modül kayıtları (Faz 2: OperationCore work item) arasındaki bağlantılar.
/// </summary>
public interface IResourceLinkService
{
    Task<ResourceLinkDto> CreateAsync(CreateResourceLinkRequest request, CancellationToken ct = default);

    Task DeleteAsync(string linkId, CancellationToken ct = default);

    Task<ResourceLinkListResult<LinkedWorkItemSummaryDto>> GetLinkedWorkItemsAsync(
        string resourceId,
        CancellationToken ct = default);

    Task<ResourceLinkListResult<LinkedResourceSummaryDto>> GetLinkedResourcesForWorkItemAsync(
        string workItemId,
        CancellationToken ct = default);
}
