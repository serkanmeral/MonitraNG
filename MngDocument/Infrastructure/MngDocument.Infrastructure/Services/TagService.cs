using System.Text.Json;
using MngDocument.Application.Contracts.Tags;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class TagService : ITagService
{
    private const string ListQuery = "skip=0&limit=500&expand=false&showHistory=false";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;

    public TagService(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<TagListResult> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var page = await _dg.QueryPageAsync(
            DmDatasets.Tags,
            new Dictionary<string, object?>(),
            ListQuery,
            Token,
            ct);

        var items = page.Items
            .Select(MapRow)
            .Where(r => r.__dataId is not null)
            .Where(r => !activeOnly || r.isActive != false)
            .Select(ToDto)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TagListResult { Items = items, Total = items.Count };
    }

    public async Task<TagDto> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var trimmed = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw DocumentException.NotFound();

        var row = await _dg.GetByIdAsync<DmTag>(DmDatasets.Tags, trimmed, Token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Etiket bulunamadı.");
        return ToDto(row);
    }

    public async Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        var name = ValidateName(request.Name);
        await EnsureNameUniqueAsync(name, exceptId: null, ct);

        var now = DateTime.UtcNow;
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["color"] = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ["isActive"] = request.IsActive,
            ["createdBy"] = _ctx.Username,
            ["createdAt"] = now,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = now
        };

        var created = await _dg.CreateAsync<DmTag>(DmDatasets.Tags, payload, Token, ct);
        return ToDto(created);
    }

    public async Task<TagDto> UpdateAsync(string id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var tagId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tagId))
            throw DocumentException.NotFound();

        _ = await GetByIdAsync(tagId, ct);
        var name = ValidateName(request.Name);
        await EnsureNameUniqueAsync(name, tagId, ct);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["color"] = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ["isActive"] = request.IsActive,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmTag>(DmDatasets.Tags, tagId, payload, Token, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var tagId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tagId))
            throw DocumentException.NotFound();

        _ = await GetByIdAsync(tagId, ct);
        await _dg.DeleteAsync(DmDatasets.Tags, tagId, Token, ct);
    }

    public async Task<IReadOnlyList<string>> NormalizeActiveTagNamesAsync(IReadOnlyList<string>? tags, CancellationToken ct = default)
    {
        if (tags is null || tags.Count == 0)
            return Array.Empty<string>();

        var catalog = await ListAsync(activeOnly: false, ct);
        var byLower = catalog.Items.ToDictionary(
            t => t.Name.Trim().ToLowerInvariant(),
            t => t,
            StringComparer.Ordinal);

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in tags)
        {
            var trimmed = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var key = trimmed.ToLowerInvariant();
            if (!byLower.TryGetValue(key, out var entry))
            {
                throw DocumentException.Validation(
                    "TAG_UNKNOWN",
                    $"Unknown tag: {trimmed}",
                    $"Bilinmeyen etiket: {trimmed}");
            }

            if (!entry.IsActive)
            {
                throw DocumentException.Validation(
                    "TAG_INACTIVE",
                    $"Tag is inactive: {entry.Name}",
                    $"Pasif etiket seçilemez: {entry.Name}");
            }

            if (seen.Add(entry.Name))
                normalized.Add(entry.Name);
        }

        return normalized;
    }

    private async Task EnsureNameUniqueAsync(string name, string? exceptId, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            DmDatasets.Tags,
            new Dictionary<string, object?> { ["name"] = name },
            "limit=20&expand=false&showHistory=false",
            Token,
            ct);

        var conflict = page.Items
            .Select(MapRow)
            .FirstOrDefault(r =>
                r.__dataId is not null
                && !string.Equals(r.__dataId, exceptId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.name?.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (conflict is not null)
        {
            throw DocumentException.Conflict(
                "TAG_NAME_EXISTS",
                "Tag name already exists.",
                "Bu etiket adı zaten kullanılıyor.");
        }
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw DocumentException.Validation(
                "TAG_NAME_REQUIRED",
                "Tag name is required.",
                "Etiket adı zorunludur.");
        }

        return trimmed;
    }

    private static DmTag MapRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmTag>(json, JsonOptions) ?? new DmTag();
    }

    private static TagDto ToDto(DmTag row) =>
        new()
        {
            Id = row.__dataId ?? string.Empty,
            Name = row.name ?? string.Empty,
            Color = row.color,
            Description = row.description,
            IsActive = row.isActive != false,
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };
}
