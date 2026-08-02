using MngLogCollector.Application.Contracts.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Abstractions.Policy;

public interface IEventLogPackageCatalogStore
{
    Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default);

    Task<IReadOnlyList<EventLogPackageDocument>> ListAsync(string databaseName, CancellationToken ct = default);

    Task<EventLogPackageDocument?> GetByNameAsync(string databaseName, string name, CancellationToken ct = default);

    Task UpsertAsync(string databaseName, EventLogPackageDocument doc, CancellationToken ct = default);

    Task<bool> DeleteByNameAsync(string databaseName, string name, CancellationToken ct = default);

    Task<EventLogCatalogMetaDocument> GetMetaAsync(string databaseName, CancellationToken ct = default);

    Task SaveMetaAsync(string databaseName, EventLogCatalogMetaDocument meta, CancellationToken ct = default);

    Task<long> CountAsync(string databaseName, CancellationToken ct = default);

    Task<EventLogHostAssignmentDocument?> GetAssignmentAsync(
        string databaseName,
        string hostKey,
        CancellationToken ct = default);

    Task UpsertAssignmentAsync(
        string databaseName,
        EventLogHostAssignmentDocument doc,
        CancellationToken ct = default);

    Task<bool> DeleteAssignmentAsync(
        string databaseName,
        string hostKey,
        CancellationToken ct = default);
}

public interface IEventLogPackageCatalogService
{
    /// <param name="hostname">When set, merges host assignment into <c>packages</c> and stamps Version for ETag.</param>
    Task<EventLogPackageCatalogResponse> GetCatalogAsync(string? hostname = null, CancellationToken ct = default);

    Task<EventLogPackageManageListResponse> ListManagedAsync(CancellationToken ct = default);

    Task<EventLogPackageManageItemDto> CreateAsync(EventLogPackageUpsertRequest request, CancellationToken ct = default);

    Task<EventLogPackageManageItemDto> UpdateAsync(string name, EventLogPackageUpsertRequest request, CancellationToken ct = default);

    Task DeleteAsync(string name, CancellationToken ct = default);

    Task<EventLogPackageCatalogResponse> PublishAsync(CancellationToken ct = default);

    Task<EventLogHostAssignmentDto> GetAssignmentAsync(string hostname, CancellationToken ct = default);

    Task<EventLogHostAssignmentDto> UpsertAssignmentAsync(
        string hostname,
        EventLogHostAssignmentUpsertRequest request,
        CancellationToken ct = default);

    Task DeleteAssignmentAsync(string hostname, CancellationToken ct = default);

    IReadOnlyList<EventLogChannelDictionaryDto> GetChannelDictionary();

    IReadOnlyList<EventLogPackagePresetDto> GetPresets();
}
