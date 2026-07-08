using MngDocument.Application.Contracts.Generation;

namespace MngDocument.Application.Interfaces;

public interface IDocumentGenerationService
{
    Task<GenerateDocumentResultDto> GenerateAsync(GenerateDocumentRequest request, CancellationToken ct = default);
    Task<GenerateDocumentResultDto> RunGenerationAsync(
        DocumentGenerationRuntimeEnvelope envelope,
        CancellationToken ct = default);
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
    Task<IReadOnlyList<DocumentContextTypeDto>> ListContextTypesAsync(CancellationToken ct = default);
    Task<DocumentContextTypeDto?> GetContextTypeAsync(string type, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentProducerDto>> ListProducersAsync(CancellationToken ct = default);
    Task<DocumentProducerDto?> GetProducerAsync(string code, CancellationToken ct = default);
}
