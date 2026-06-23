using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface IDocumentTemplateService
{
    Task<TemplateListResult> ListAsync(CancellationToken ct = default);
    Task<TemplateDetailDto> GetByIdAsync(string id, CancellationToken ct = default);
    Task<TemplateDetailDto> CreateFromSourceAsync(CreateTemplateFromSourceRequest request, CancellationToken ct = default);
    Task<DocxStructureDto> GetSourceStructureAsync(string resourceId, CancellationToken ct = default);
    Task<TemplateDetailDto> UpdateParametersAsync(string id, UpdateTemplateParametersRequest request, CancellationToken ct = default);
}
