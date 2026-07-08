using MngDocument.Application.Contracts.CoverPages;

namespace MngDocument.Application.Interfaces;

public interface ICoverPageService
{
    Task<CoverPageListResult> ListAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<CoverPageDto> GetByIdAsync(string id, CancellationToken ct = default);
    Task<CoverPageDto?> TryGetByIdAsync(string id, CancellationToken ct = default);
    Task<CoverPageDto?> TryGetByCodeAsync(string code, CancellationToken ct = default);
    Task<CoverPageDto> CreateAsync(CreateCoverPageRequest request, CancellationToken ct = default);
    Task<CoverPageDto> UpdateAsync(string id, UpdateCoverPageRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task EnsureActiveAsync(string id, CancellationToken ct = default);

    Task<CoverPageResolveResult> ResolveAsync(
        bool includeCoverPage,
        string? requestCoverPageId,
        string? templateDefaultCoverPageId,
        CancellationToken ct = default);
}
