using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Application.Interfaces;

public interface IDocumentGenerationService
{
    Task<GenerateDocumentResultDto> GenerateAsync(GenerateDocumentRequest request, CancellationToken ct = default);
    Task<GenerateDocumentResultDto> GenerateFromTemplateAsync(
        string templateId,
        GenerateFromTemplateRequest request,
        CancellationToken ct = default);
    Task<DocumentGenerationPreviewDto> PreviewFromTemplateAsync(
        string templateId,
        PreviewFromTemplateRequest? request,
        CancellationToken ct = default);
    Task<TemplateGenerationPreviewSessionDto> CreatePreviewSessionFromTemplateAsync(
        string templateId,
        PreviewFromTemplateRequest? request,
        CancellationToken ct = default);
    Task<DocumentGenerationStatusDto> GetStatusAsync(string profileCode, string contextId, CancellationToken ct = default);
    Task<DocumentGenerationPreviewDto> PreviewAsync(string profileCode, string contextId, CancellationToken ct = default);
    IReadOnlyList<DocumentContextTypeDto> ListContextTypes();
    DocumentContextTypeDto? GetContextType(string type);
}
