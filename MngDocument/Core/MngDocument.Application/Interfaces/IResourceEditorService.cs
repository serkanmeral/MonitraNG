using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface IResourceEditorService
{
    Task<ResourceEditorSessionDto> CreateEditorSessionAsync(string resourceId, CancellationToken ct = default);

    /// <summary>Belirli bir sürümü salt okunur Collabora oturumunda açar.</summary>
    Task<ResourceEditorSessionDto> CreateVersionPreviewSessionAsync(
        string resourceId,
        int versionNumber,
        CancellationToken ct = default);

    Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default);

    Task<byte[]> GetFileContentsAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default);

    Task SaveFileContentsAsync(
        string resourceId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default);
}
