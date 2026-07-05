using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface ILetterheadEditorService
{
    Task<LetterheadDesignSessionDto> CreateDesignSessionAsync(string letterheadId, CancellationToken ct = default);

    Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string letterheadId,
        WopiSession session,
        CancellationToken ct = default);

    Task<byte[]> GetFileContentsAsync(
        string letterheadId,
        WopiSession session,
        CancellationToken ct = default);

    Task SaveFileContentsAsync(
        string letterheadId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default);
}
