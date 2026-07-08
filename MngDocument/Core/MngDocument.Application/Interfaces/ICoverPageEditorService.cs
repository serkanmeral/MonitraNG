using MngDocument.Application.Contracts.CoverPages;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface ICoverPageEditorService
{
    Task<CoverPageDesignSessionDto> CreateDesignSessionAsync(string coverPageId, CancellationToken ct = default);

    Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string coverPageId,
        WopiSession session,
        CancellationToken ct = default);

    Task<byte[]> GetFileContentsAsync(
        string coverPageId,
        WopiSession session,
        CancellationToken ct = default);

    Task SaveFileContentsAsync(
        string coverPageId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default);
}
