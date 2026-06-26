using MngDocument.Application.Contracts.Rendering;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface IDocumentTemplateService
{
    Task<TemplateListResult> ListAsync(string? categoryId = null, CancellationToken ct = default);
    Task<TemplateDetailDto> GetByIdAsync(string id, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<TemplateDetailDto> UpdateMetadataAsync(string id, UpdateTemplateMetadataRequest request, CancellationToken ct = default);
    Task<TemplateDetailDto> UpdateLetterheadAsync(string id, UpdateTemplateLetterheadRequest request, CancellationToken ct = default);
    Task<TemplateDetailDto> UpdateFooterAsync(string id, UpdateTemplateFooterRequest request, CancellationToken ct = default);
    Task<TemplateDetailDto> UpdatePageStructureAsync(string id, UpdateTemplatePageStructureRequest request, CancellationToken ct = default);
    Task<TemplateDetailDto> PublishAsync(string id, CancellationToken ct = default);
    Task<TemplateDetailDto> CreateFromSourceAsync(CreateTemplateFromSourceRequest request, CancellationToken ct = default);
    Task<TemplateDetailDto> CreateFromReferenceAsync(CreateTemplateFromReferenceRequest request, CancellationToken ct = default);
    Task<TemplateDetailDto> DuplicateAsync(string id, DuplicateTemplateRequest request, CancellationToken ct = default);
    Task<DocxStructureDto> GetSourceStructureAsync(string resourceId, CancellationToken ct = default);
    Task<DocxStructureDto> GetTemplateSourceStructureAsync(string templateId, CancellationToken ct = default);
    Task<TemplateDetailDto> UpdateParametersAsync(string id, UpdateTemplateParametersRequest request, CancellationToken ct = default);
    Task<byte[]> RenderTemplatePdfAsync(string id, RenderTemplatePdfRequest? request, CancellationToken ct = default);
}
