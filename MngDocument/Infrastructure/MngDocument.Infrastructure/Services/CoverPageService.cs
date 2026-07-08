using System.Text.Json;
using MngDocument.Application.Contracts.CoverPages;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;

namespace MngDocument.Infrastructure.Services;

public sealed class CoverPageService : ICoverPageService
{
    private const string ListQuery = "skip=0&limit=500&expand=false&showHistory=false";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;

    public CoverPageService(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<CoverPageListResult> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var page = await _dg.QueryPageAsync(
            DmDatasets.CoverPages,
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

        return new CoverPageListResult { Items = items, Total = items.Count };
    }

    public async Task<CoverPageDto> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var row = await TryGetByIdAsync(id, ct);
        return row ?? throw DocumentException.NotFound("Kapak sayfası bulunamadı.");
    }

    public async Task<CoverPageDto?> TryGetByIdAsync(string id, CancellationToken ct = default)
    {
        var trimmed = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var row = await _dg.GetByIdAsync<DmCoverPage>(DmDatasets.CoverPages, trimmed, Token, ct);
        return row is null || row.__dataId is null ? null : ToDto(row);
    }

    public async Task<CoverPageDto?> TryGetByCodeAsync(string code, CancellationToken ct = default)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var filter = new Dictionary<string, object?> { ["code"] = trimmed };
        var page = await _dg.QueryPageAsync(DmDatasets.CoverPages, filter, ListQuery, Token, ct);
        var row = page.Items.Select(MapRow).FirstOrDefault(r =>
            string.Equals(r.code, trimmed, StringComparison.OrdinalIgnoreCase));
        return row is null || row.__dataId is null ? null : ToDto(row);
    }

    public async Task<CoverPageDto> CreateAsync(CreateCoverPageRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request.Name, request.Code);
        await EnsureCodeUniqueAsync(request.Code, null, ct);

        if (request.IsDefault)
            await ClearDefaultFlagAsync(exceptId: null, ct);

        var payload = BuildPayload(
            request.Name,
            request.Code,
            request.Description,
            request.IsDefault,
            request.IsActive,
            request.Definition,
            request.Settings,
            isCreate: true);
        var created = await _dg.CreateAsync<DmCoverPage>(DmDatasets.CoverPages, payload, Token, ct);
        return ToDto(created);
    }

    public async Task<CoverPageDto> UpdateAsync(string id, UpdateCoverPageRequest request, CancellationToken ct = default)
    {
        var coverPageId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(coverPageId))
            throw DocumentException.NotFound();

        _ = await GetByIdAsync(coverPageId, ct);
        ValidateRequest(request.Name, request.Code);
        await EnsureCodeUniqueAsync(request.Code, coverPageId, ct);

        if (request.IsDefault)
            await ClearDefaultFlagAsync(exceptId: coverPageId, ct);

        var payload = BuildPayload(
            request.Name,
            request.Code,
            request.Description,
            request.IsDefault,
            request.IsActive,
            request.Definition,
            request.Settings,
            isCreate: false);
        var updated = await _dg.UpdateAsync<DmCoverPage>(DmDatasets.CoverPages, coverPageId, payload, Token, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var coverPageId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(coverPageId))
            throw DocumentException.NotFound();

        _ = await GetByIdAsync(coverPageId, ct);
        await _dg.DeleteAsync(DmDatasets.CoverPages, coverPageId, Token, ct);
    }

    public async Task EnsureActiveAsync(string id, CancellationToken ct = default)
    {
        var dto = await GetByIdAsync(id, ct);
        if (!dto.IsActive)
        {
            throw DocumentException.Validation(
                "COVER_PAGE_INACTIVE",
                "Cover page is inactive.",
                "Seçilen kapak sayfası pasif durumda.");
        }
    }

    public async Task<CoverPageResolveResult> ResolveAsync(
        bool includeCoverPage,
        string? requestCoverPageId,
        string? templateDefaultCoverPageId,
        CancellationToken ct = default)
    {
        if (!includeCoverPage)
            return new CoverPageResolveResult();

        if (!string.IsNullOrWhiteSpace(requestCoverPageId))
        {
            var fromRequest = await TryGetByIdAsync(requestCoverPageId, ct);
            if (fromRequest is { IsActive: true })
                return BuildResolve(fromRequest);
        }

        if (!string.IsNullOrWhiteSpace(templateDefaultCoverPageId))
        {
            var fromTemplate = await TryGetByIdAsync(templateDefaultCoverPageId, ct);
            if (fromTemplate is { IsActive: true })
                return BuildResolve(fromTemplate);
        }

        var all = await ListAsync(activeOnly: true, ct);
        var defaultEntry = all.Items.FirstOrDefault(x => x.IsDefault)
                           ?? all.Items.FirstOrDefault();
        return defaultEntry is null ? new CoverPageResolveResult() : BuildResolve(defaultEntry);
    }

    private async Task ClearDefaultFlagAsync(string? exceptId, CancellationToken ct)
    {
        var all = await ListAsync(activeOnly: false, ct);
        foreach (var item in all.Items.Where(x => x.IsDefault && !string.Equals(x.Id, exceptId, StringComparison.OrdinalIgnoreCase)))
        {
            var payload = new Dictionary<string, object?>
            {
                ["isDefault"] = false,
                ["updatedBy"] = _ctx.Username,
                ["updatedAt"] = DateTime.UtcNow
            };
            await _dg.UpdateAsync<DmCoverPage>(DmDatasets.CoverPages, item.Id, payload, Token, ct);
        }
    }

    private async Task EnsureCodeUniqueAsync(string code, string? exceptId, CancellationToken ct)
    {
        var existing = await TryGetByCodeAsync(code, ct);
        if (existing is null || string.Equals(existing.Id, exceptId, StringComparison.OrdinalIgnoreCase))
            return;

        throw DocumentException.Conflict(
            "COVER_PAGE_CODE_EXISTS",
            "Cover page code already exists.",
            "Kapak sayfası kodu zaten kullanılıyor.");
    }

    private static void ValidateRequest(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DocumentException.Validation(
                "COVER_PAGE_NAME_REQUIRED",
                "Name is required.",
                "Kapak sayfası adı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "COVER_PAGE_CODE_REQUIRED",
                "Code is required.",
                "Kapak sayfası kodu zorunludur.");
        }
    }

    private Dictionary<string, object?> BuildPayload(
        string name,
        string code,
        string? description,
        bool isDefault,
        bool isActive,
        CoverPageDefinitionDto definition,
        CoverPageSettingsDto? settings,
        bool isCreate)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name.Trim(),
            ["code"] = code.Trim(),
            ["description"] = description,
            ["isDefault"] = isDefault,
            ["isActive"] = isActive,
            ["coverPageJson"] = CoverPageSettingsSerializer.SerializeDefinition(definition),
            ["settingsJson"] = CoverPageSettingsSerializer.Serialize(CoverPageSettingsSerializer.Normalize(settings)),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        if (isCreate)
        {
            payload["createdBy"] = _ctx.Username;
            payload["createdAt"] = DateTime.UtcNow;
        }

        return payload;
    }

    private static CoverPageResolveResult BuildResolve(CoverPageDto dto) => new()
    {
        CoverPage = dto,
        CoverPageId = dto.Id,
        CoverPageCode = dto.Code,
        CoverPageName = dto.Name
    };

    private static DmCoverPage MapRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmCoverPage>(json, JsonOptions) ?? new DmCoverPage();
    }

    private static CoverPageDto ToDto(DmCoverPage row)
    {
        var (_, designNameFromField) = DgFileFieldReader.Read(row);
        var designStoragePath = CoverPageDesignFileLoader.ResolveDesignPath(row);
        var designFileName = row.designFileName ?? designNameFromField;

        return new CoverPageDto
        {
            Id = row.__dataId ?? string.Empty,
            Name = row.name ?? string.Empty,
            Code = row.code ?? string.Empty,
            Description = row.description,
            IsDefault = row.isDefault == true,
            IsActive = row.isActive != false,
            Definition = CoverPageSettingsSerializer.ParseDefinition(row.coverPageJson),
            Settings = CoverPageSettingsSerializer.Parse(row.settingsJson),
            DesignStoragePath = designStoragePath,
            DesignFileName = designFileName,
            HasDesign = !string.IsNullOrWhiteSpace(designStoragePath),
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };
    }
}
