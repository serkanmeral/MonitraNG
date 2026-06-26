using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface ITemplateCategoryService
{
    Task<IReadOnlyList<TemplateCategoryTreeNodeDto>> GetTreeAsync(CancellationToken ct = default);
    Task<TemplateCategoryDto> CreateAsync(CreateTemplateCategoryRequest request, CancellationToken ct = default);
    Task<TemplateCategoryDto> RenameAsync(string id, RenameTemplateCategoryRequest request, CancellationToken ct = default);
    Task<TemplateCategoryDto> MoveAsync(string id, MoveTemplateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
