using System.Text.Json;
using System.Text.Json.Serialization;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class TemplateCategoryService : ITemplateCategoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string ListQuery = "limit=1000&expand=false&showHistory=false&sort=name";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;

    public TemplateCategoryService(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<IReadOnlyList<TemplateCategoryTreeNodeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);
        return BuildTree(all);
    }

    public async Task<TemplateCategoryDto> CreateAsync(CreateTemplateCategoryRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        var parentId = NormalizeParentId(request.ParentId);
        if (parentId is not null)
            await LoadOrThrowAsync(parentId, ct);

        var ancestorIds = await ResolveAncestorsForChildAsync(parentId, ct);
        var now = DateTime.UtcNow;
        var payload = new Dictionary<string, object?>
        {
            ["parentId"] = parentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Name.Trim(),
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ["sortOrder"] = 0,
            ["status"] = "active",
            ["createdBy"] = _ctx.Username,
            ["createdAt"] = now,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = now
        };

        var created = await _dg.CreateAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, payload, Token, ct);
        return ToDto(created);
    }

    public async Task<TemplateCategoryDto> RenameAsync(string id, RenameTemplateCategoryRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        var existing = await LoadOrThrowAsync(id, ct);
        var payload = new Dictionary<string, object?>
        {
            ["name"] = request.Name.Trim(),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };
        var updated = await _dg.UpdateAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, id, payload, Token, ct);
        return ToDto(updated);
    }

    public async Task<TemplateCategoryDto> MoveAsync(string id, MoveTemplateCategoryRequest request, CancellationToken ct = default)
    {
        var node = await LoadOrThrowAsync(id, ct);
        var newParentId = NormalizeParentId(request.NewParentId);

        if (newParentId == id)
        {
            throw DocumentException.Validation(
                "INVALID_MOVE",
                "Cannot move a category into itself.",
                "Kategori kendi içine taşınamaz.");
        }

        List<string> newAncestors;
        if (newParentId is null)
        {
            newAncestors = new List<string>();
        }
        else
        {
            var newParent = await LoadOrThrowAsync(newParentId, ct);
            if ((newParent.ancestorIds ?? new List<string>()).Contains(id))
            {
                throw DocumentException.Validation(
                    "INVALID_MOVE",
                    "Cannot move a category into its own descendant.",
                    "Kategori kendi alt kategorisine taşınamaz.");
            }

            newAncestors = new List<string>(newParent.ancestorIds ?? new List<string>()) { newParentId };
        }

        var payload = new Dictionary<string, object?>
        {
            ["parentId"] = newParentId,
            ["ancestorIds"] = newAncestors,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };
        var updated = await _dg.UpdateAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, id, payload, Token, ct);
        await ReindexDescendantsAsync(id, newAncestors, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await LoadOrThrowAsync(id, ct);

        var childMatch = new Dictionary<string, object?> { ["parentId"] = id };
        var childPage = await _dg.QueryPageAsync(DmDatasets.TemplateCategories, childMatch, "limit=1", Token, ct);
        if (childPage.Total > 0)
        {
            throw DocumentException.Conflict(
                "CATEGORY_NOT_EMPTY",
                "Category has child categories.",
                "Kategori alt kategoriler içeriyor.");
        }

        var tplMatch = new Dictionary<string, object?> { ["categoryId"] = id };
        var tplPage = await _dg.QueryPageAsync(DmDatasets.DocumentTemplates, tplMatch, "limit=1", Token, ct);
        if (tplPage.Total > 0)
        {
            throw DocumentException.Conflict(
                "CATEGORY_HAS_TEMPLATES",
                "Category has templates.",
                "Kategoride şablon kayıtları var.");
        }

        await _dg.DeleteAsync(DmDatasets.TemplateCategories, id, Token, ct);
    }

    private async Task<IReadOnlyList<DmTemplateCategory>> LoadAllAsync(CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            DmDatasets.TemplateCategories,
            new Dictionary<string, object?>(),
            ListQuery,
            Token,
            ct);
        return page.Items.Select(MapRow).Where(c => c.__dataId is not null).ToList();
    }

    private async Task<DmTemplateCategory> LoadOrThrowAsync(string id, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, id, Token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Kategori bulunamadı.");
        return row;
    }

    private async Task<List<string>> ResolveAncestorsForChildAsync(string? parentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            return new List<string>();

        var parent = await LoadOrThrowAsync(parentId, ct);
        return new List<string>(parent.ancestorIds ?? new List<string>()) { parentId };
    }

    private async Task ReindexDescendantsAsync(string id, List<string> newAncestorsForNode, CancellationToken ct)
    {
        var match = new Dictionary<string, object?> { ["ancestorIds"] = id };
        var page = await _dg.QueryPageAsync(DmDatasets.TemplateCategories, match, ListQuery, Token, ct);
        foreach (var row in page.Items.Select(MapRow))
        {
            if (row.__dataId is null || row.__dataId == id)
                continue;

            var oldAncestors = row.ancestorIds ?? new List<string>();
            var idx = oldAncestors.IndexOf(id);
            if (idx < 0)
                continue;

            var suffix = oldAncestors.Skip(idx).ToList();
            var newAncestors = new List<string>(newAncestorsForNode);
            newAncestors.AddRange(suffix);

            var payload = new Dictionary<string, object?>
            {
                ["ancestorIds"] = newAncestors,
                ["updatedBy"] = _ctx.Username,
                ["updatedAt"] = DateTime.UtcNow
            };
            await _dg.UpdateAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, row.__dataId, payload, Token, ct);
            await ReindexDescendantsAsync(row.__dataId, newAncestors, ct);
        }
    }

    private static IReadOnlyList<TemplateCategoryTreeNodeDto> BuildTree(IReadOnlyList<DmTemplateCategory> categories)
    {
        var nodes = categories.ToDictionary(
            c => c.__dataId!,
            c => new TemplateCategoryTreeNodeDto
            {
                Id = c.__dataId!,
                Name = c.name ?? string.Empty,
                ParentId = c.parentId
            });

        var roots = new List<TemplateCategoryTreeNodeDto>();
        foreach (var cat in categories)
        {
            if (cat.__dataId is null)
                continue;

            var node = nodes[cat.__dataId];
            if (!string.IsNullOrWhiteSpace(cat.parentId) && nodes.TryGetValue(cat.parentId!, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        SortTree(roots);
        return roots;
    }

    private static void SortTree(List<TemplateCategoryTreeNodeDto> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        foreach (var node in nodes)
            SortTree(node.Children);
    }

    private static DmTemplateCategory MapRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmTemplateCategory>(json, JsonOptions) ?? new DmTemplateCategory();
    }

    private static TemplateCategoryDto ToDto(DmTemplateCategory row) =>
        new()
        {
            Id = row.__dataId ?? string.Empty,
            ParentId = row.parentId,
            AncestorIds = row.ancestorIds ?? new List<string>(),
            Name = row.name ?? string.Empty,
            Description = row.description,
            SortOrder = row.sortOrder ?? 0,
            Status = row.status ?? "active",
            CreatedAt = row.createdAt,
            CreatedBy = row.createdBy,
            UpdatedAt = row.updatedAt
        };

    private static string? NormalizeParentId(string? parentId) =>
        string.IsNullOrWhiteSpace(parentId) ? null : parentId.Trim();

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DocumentException.Validation(
                "NAME_REQUIRED",
                "Name is required.",
                "İsim zorunludur.");
        }
    }
}
