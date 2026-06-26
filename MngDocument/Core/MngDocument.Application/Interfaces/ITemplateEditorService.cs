using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface ITemplateEditorService
{
    Task<TemplateDetailDto> CreateBlankAsync(CreateBlankTemplateRequest request, CancellationToken ct = default);

    Task<TemplateEditorSessionDto> CreateEditorSessionAsync(string templateId, CancellationToken ct = default);

    Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(string templateId, WopiSession session, CancellationToken ct = default);

    Task<byte[]> GetFileContentsAsync(string templateId, WopiSession session, CancellationToken ct = default);

    Task SaveFileContentsAsync(
        string templateId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default);
}
