using System.Text.Json;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure.Services;

public sealed class LetterheadService : ILetterheadService
{
    private const string ListQuery = "skip=0&limit=500&expand=false&showHistory=false";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;

    public LetterheadService(IMngDataGatewayClient dg, IRequestContext ctx)
    {
        _dg = dg;
        _ctx = ctx;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<LetterheadListResult> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var page = await _dg.QueryPageAsync(
            DmDatasets.Letterheads,
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

        return new LetterheadListResult { Items = items, Total = items.Count };
    }

    public async Task<LetterheadDto> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var row = await TryGetByIdAsync(id, ct);
        return row ?? throw DocumentException.NotFound("Antet bulunamadı.");
    }

    public async Task<LetterheadDto?> TryGetByIdAsync(string id, CancellationToken ct = default)
    {
        var trimmed = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var row = await _dg.GetByIdAsync<DmLetterhead>(DmDatasets.Letterheads, trimmed, Token, ct);
        return row is null || row.__dataId is null ? null : ToDto(row);
    }

    public async Task<LetterheadDto?> TryGetByCodeAsync(string code, CancellationToken ct = default)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var filter = new Dictionary<string, object?> { ["code"] = trimmed };
        var page = await _dg.QueryPageAsync(DmDatasets.Letterheads, filter, ListQuery, Token, ct);
        var row = page.Items.Select(MapRow).FirstOrDefault(r =>
            string.Equals(r.code, trimmed, StringComparison.OrdinalIgnoreCase));
        return row is null || row.__dataId is null ? null : ToDto(row);
    }

    public async Task<LetterheadDto> CreateAsync(CreateLetterheadRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request.Name, request.Code, request.Letterhead);
        await EnsureCodeUniqueAsync(request.Code, null, ct);

        if (request.IsDefault)
            await ClearDefaultFlagAsync(exceptId: null, ct);

        var payload = BuildPayload(
            request.Name,
            request.Code,
            request.Description,
            request.IsDefault,
            request.IsActive,
            request.Letterhead,
            request.Settings,
            isCreate: true);
        var created = await _dg.CreateAsync<DmLetterhead>(DmDatasets.Letterheads, payload, Token, ct);
        return ToDto(created);
    }

    public async Task<LetterheadDto> UpdateAsync(string id, UpdateLetterheadRequest request, CancellationToken ct = default)
    {
        var letterheadId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(letterheadId))
            throw DocumentException.NotFound();

        _ = await GetByIdAsync(letterheadId, ct);
        ValidateRequest(request.Name, request.Code, request.Letterhead);
        await EnsureCodeUniqueAsync(request.Code, letterheadId, ct);

        if (request.IsDefault)
            await ClearDefaultFlagAsync(exceptId: letterheadId, ct);

        var payload = BuildPayload(
            request.Name,
            request.Code,
            request.Description,
            request.IsDefault,
            request.IsActive,
            request.Letterhead,
            request.Settings,
            isCreate: false);
        var updated = await _dg.UpdateAsync<DmLetterhead>(DmDatasets.Letterheads, letterheadId, payload, Token, ct);
        return ToDto(updated);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var letterheadId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(letterheadId))
            throw DocumentException.NotFound();

        _ = await GetByIdAsync(letterheadId, ct);
        await _dg.DeleteAsync(DmDatasets.Letterheads, letterheadId, Token, ct);
    }

    public async Task EnsureActiveAsync(string id, CancellationToken ct = default)
    {
        var dto = await GetByIdAsync(id, ct);
        if (!dto.IsActive)
        {
            throw DocumentException.Validation(
                "LETTERHEAD_INACTIVE",
                "Letterhead is inactive.",
                "Seçilen antet pasif durumda.");
        }
    }

    public async Task<LetterheadResolveResult> ResolveAsync(
        string? templateDefaultLetterheadId,
        TemplateLetterheadDto? legacyEmbedded,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(templateDefaultLetterheadId))
        {
            var fromTemplate = await TryGetByIdAsync(templateDefaultLetterheadId, ct);
            if (fromTemplate is { IsActive: true, Letterhead.Enabled: true })
                return BuildResolve(fromTemplate);
        }

        var all = await ListAsync(activeOnly: true, ct);
        var defaultEntry = all.Items.FirstOrDefault(x => x.IsDefault && x.Letterhead.Enabled)
                           ?? all.Items.FirstOrDefault(x => x.Letterhead.Enabled);
        if (defaultEntry is not null)
            return BuildResolve(defaultEntry);

        if (legacyEmbedded is { Enabled: true })
        {
            return new LetterheadResolveResult
            {
                Letterhead = legacyEmbedded,
                LetterheadId = null,
                LetterheadCode = null,
                LetterheadName = null
            };
        }

        return new LetterheadResolveResult();
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
            await _dg.UpdateAsync<DmLetterhead>(DmDatasets.Letterheads, item.Id, payload, Token, ct);
        }
    }

    private async Task EnsureCodeUniqueAsync(string code, string? exceptId, CancellationToken ct)
    {
        var existing = await TryGetByCodeAsync(code, ct);
        if (existing is null || string.Equals(existing.Id, exceptId, StringComparison.OrdinalIgnoreCase))
            return;

        throw DocumentException.Conflict(
            "LETTERHEAD_CODE_EXISTS",
            "Letterhead code already exists.",
            "Antet kodu zaten kullanılıyor.");
    }

    private static void ValidateRequest(string name, string code, TemplateLetterheadDto letterhead)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DocumentException.Validation(
                "LETTERHEAD_NAME_REQUIRED",
                "Name is required.",
                "Antet adı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "LETTERHEAD_CODE_REQUIRED",
                "Code is required.",
                "Antet kodu zorunludur.");
        }

        if (letterhead is not { Enabled: true })
        {
            throw DocumentException.Validation(
                "LETTERHEAD_DISABLED",
                "Letterhead must be enabled.",
                "Antet tanımı etkin olmalıdır.");
        }
    }

    private Dictionary<string, object?> BuildPayload(
        string name,
        string code,
        string? description,
        bool isDefault,
        bool isActive,
        TemplateLetterheadDto letterhead,
        LetterheadSettingsDto? settings,
        bool isCreate)
    {
        var model = TemplateModelSerializer.ToLetterheadModel(letterhead) ?? new TemplateLetterheadModel { Enabled = true };
        var normalizedSettings = settings is null
            ? LetterheadSettingsSerializer.CreateDefault()
            : LetterheadSettingsSerializer.Normalize(settings);
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name.Trim(),
            ["code"] = code.Trim(),
            ["description"] = description,
            ["isDefault"] = isDefault,
            ["isActive"] = isActive,
            ["letterheadJson"] = JsonSerializer.Serialize(model, JsonOptions),
            ["settingsJson"] = LetterheadSettingsSerializer.Serialize(normalizedSettings),
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

    private static LetterheadResolveResult BuildResolve(LetterheadDto dto)
    {
        var settings = LetterheadSettingsSerializer.Normalize(dto.Settings);
        var baseModel = TemplateModelSerializer.ToLetterheadModel(dto.Letterhead) ?? new TemplateLetterheadModel { Enabled = true };
        var effectiveModel = LetterheadSettingsSerializer.ApplyHeaderFields(baseModel, settings.HeaderFields);

        return new LetterheadResolveResult
        {
            Letterhead = TemplateModelSerializer.ToLetterheadDto(effectiveModel) ?? dto.Letterhead,
            Settings = settings,
            Footer = settings.Footer,
            PageLayout = settings.PageLayout,
            LetterheadId = dto.Id,
            LetterheadCode = dto.Code,
            LetterheadName = dto.Name
        };
    }

    private static DmLetterhead MapRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmLetterhead>(json, JsonOptions) ?? new DmLetterhead();
    }

    private static LetterheadDto ToDto(DmLetterhead row)
    {
        TemplateLetterheadDto letterhead = new() { Enabled = true };
        if (!string.IsNullOrWhiteSpace(row.letterheadJson))
        {
            try
            {
                var model = JsonSerializer.Deserialize<TemplateLetterheadModel>(row.letterheadJson, JsonOptions);
                letterhead = TemplateModelSerializer.ToLetterheadDto(model) ?? letterhead;
            }
            catch
            {
                // keep defaults
            }
        }

        var (designPathFromField, designNameFromField) = DgFileFieldReader.Read(row);
        var designStoragePath = !string.IsNullOrWhiteSpace(row.designStoragePath)
            ? row.designStoragePath
            : designPathFromField;
        var designFileName = row.designFileName ?? designNameFromField;

        return new LetterheadDto
        {
            Id = row.__dataId ?? string.Empty,
            Name = row.name ?? string.Empty,
            Code = row.code ?? string.Empty,
            Description = row.description,
            IsDefault = row.isDefault == true,
            IsActive = row.isActive != false,
            Letterhead = letterhead,
            Settings = LetterheadSettingsSerializer.Parse(row.settingsJson),
            DesignStoragePath = designStoragePath,
            DesignFileName = designFileName,
            HasDesign = !string.IsNullOrWhiteSpace(designStoragePath),
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };
    }
}
