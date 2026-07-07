using System.Text.Json;
using System.Text.Json.Serialization;
using MngDocument.Application.Contracts.Rendering;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;
using MngDocument.Infrastructure.Services.Generation;

namespace MngDocument.Infrastructure.Services;

public sealed class DocumentTemplateService : IDocumentTemplateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string SchemaVersion = "1.0";
    private const string ListQuery = "limit=200&expand=false&showHistory=false&sort=-updatedAt";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IResourceService _resources;
    private readonly IDocumentRenderService _render;
    private readonly ITemplateBrandingApplier _brandingApplier;
    private readonly ILetterheadService _letterheads;

    public DocumentTemplateService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IResourceService resources,
        IDocumentRenderService render,
        ITemplateBrandingApplier brandingApplier,
        ILetterheadService letterheads)
    {
        _dg = dg;
        _ctx = ctx;
        _resources = resources;
        _render = render;
        _brandingApplier = brandingApplier;
        _letterheads = letterheads;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<TemplateListResult> ListAsync(string? categoryId = null, CancellationToken ct = default)
    {
        var match = new Dictionary<string, object?>();
        var cat = categoryId?.Trim();
        if (!string.IsNullOrWhiteSpace(cat))
            match["categoryId"] = cat;

        var page = await _dg.QueryPageAsync(
            DmDatasets.DocumentTemplates,
            match,
            ListQuery,
            Token,
            ct);

        var items = page.Items
            .Select(MapSummaryRow)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt ?? DateTime.MinValue)
            .ToList();

        return new TemplateListResult { Items = items, Total = page.Total };
    }

    public async Task<TemplateDetailDto> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var row = await LoadTemplateOrThrowAsync(id, ct);
        return ToDetail(row);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        await LoadTemplateOrThrowAsync(templateId, ct);
        await _dg.DeleteAsync(DmDatasets.DocumentTemplates, templateId, Token, ct);
    }

    public async Task<TemplateDetailDto> UpdateMetadataAsync(
        string id,
        UpdateTemplateMetadataRequest request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        TemplateDraftGuard.EnsureDraft(existing);

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DocumentException.Validation(
                "NAME_REQUIRED",
                "Template name is required.",
                "Belge adı zorunludur.");
        }

        var code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "CODE_REQUIRED",
                "Template code is required.",
                "Belge kodu zorunludur.");
        }

        var categoryId = existing.categoryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            throw DocumentException.Validation(
                "CATEGORY_REQUIRED",
                "Template category is missing.",
                "Belge kategorisi bulunamadı.");
        }

        await EnsureCodeUniqueInCategoryAsync(categoryId, code, templateId, ct);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["code"] = code,
            ["sourceFileName"] = TemplateFileNameHelper.ResolveDisplayFileName(name, code, existing.sourceFileName),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        updated = await RefreshBrandingDocxAsync(updated, ct);
        return ToDetail(updated);
    }

    public async Task<TemplateDetailDto> UpdateLetterheadAsync(
        string id,
        UpdateTemplateLetterheadRequest request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        TemplateDraftGuard.EnsureDraft(existing);

        var letterheadDto = request.Letterhead ?? new TemplateLetterheadDto();
        var letterheadModel = TemplateModelSerializer.ToLetterheadModel(letterheadDto)
                              ?? new TemplateLetterheadModel();

        var model = TemplateModelSerializer.Parse(existing.modelJson);
        if (letterheadModel.Enabled)
        {
            model.Letterhead = letterheadModel;
            model.Parameters = TemplateModelSerializer.EnsureLetterheadParameters(
                letterheadModel,
                model.Parameters);
        }
        else
        {
            model.Letterhead = new TemplateLetterheadModel { Enabled = false };
            model.Parameters = TemplateModelSerializer.RemoveSystemLetterheadParameters(model.Parameters);
        }

        model.SchemaVersion = TemplateModelSerializer.CurrentSchemaVersion;

        var payload = new Dictionary<string, object?>
        {
            ["modelJson"] = TemplateModelSerializer.Serialize(model),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        updated = await RefreshBrandingDocxAsync(updated, ct);
        return ToDetail(updated);
    }

    public async Task<TemplateDetailDto> UpdateFooterAsync(
        string id,
        UpdateTemplateFooterRequest request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        TemplateDraftGuard.EnsureDraft(existing);

        var footerDto = request.Footer ?? new TemplateFooterDto();
        var footerModel = TemplateModelSerializer.ToFooterModel(footerDto)
                          ?? new TemplateFooterModel();

        var model = TemplateModelSerializer.Parse(existing.modelJson);
        model.Footer = footerModel.Enabled
            ? footerModel
            : new TemplateFooterModel { Enabled = false };

        model.SchemaVersion = TemplateModelSerializer.CurrentSchemaVersion;

        var payload = new Dictionary<string, object?>
        {
            ["modelJson"] = TemplateModelSerializer.Serialize(model),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        updated = await RefreshBrandingDocxAsync(updated, ct);
        return ToDetail(updated);
    }

    public async Task<TemplateDetailDto> UpdatePageStructureAsync(
        string id,
        UpdateTemplatePageStructureRequest request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        TemplateDraftGuard.EnsureDraft(existing);

        var model = TemplateModelSerializer.Parse(existing.modelJson);

        if (request.DefaultLetterheadId is not null)
        {
            var defaultId = string.IsNullOrWhiteSpace(request.DefaultLetterheadId)
                ? null
                : request.DefaultLetterheadId.Trim();
            if (defaultId is not null)
                await _letterheads.EnsureActiveAsync(defaultId, ct);

            model.DefaultLetterheadId = defaultId;
            model.Letterhead = null;

            if (defaultId is not null)
            {
                var resolved = await _letterheads.ResolveAsync(defaultId, null, ct);
                if (resolved.Letterhead is { Enabled: true })
                {
                    var letterheadModel = TemplateModelSerializer.ToLetterheadModel(resolved.Letterhead);
                    if (letterheadModel is not null)
                    {
                        model.Parameters = TemplateModelSerializer.EnsureLetterheadParameters(
                            letterheadModel,
                            model.Parameters);
                    }
                }
            }
            else
            {
                model.Parameters = TemplateModelSerializer.RemoveSystemLetterheadParameters(model.Parameters);
            }
        }

        if (request.Footer is not null)
        {
            var footerModel = TemplateModelSerializer.ToFooterModel(request.Footer)
                              ?? new TemplateFooterModel();
            model.Footer = footerModel.Enabled
                ? footerModel
                : new TemplateFooterModel { Enabled = false };
        }

        if (request.PageLayout is not null)
        {
            model.PageLayout = TemplateModelSerializer.ToPageLayoutModel(request.PageLayout)
                               ?? TemplatePageLayoutModel.CreateDefault();
        }
        else
        {
            model.PageLayout ??= TemplatePageLayoutModel.CreateDefault();
        }

        model.SchemaVersion = TemplateModelSerializer.CurrentSchemaVersion;

        var payload = new Dictionary<string, object?>
        {
            ["modelJson"] = TemplateModelSerializer.Serialize(model),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        updated = await RefreshBrandingDocxAsync(updated, ct);
        return ToDetail(updated);
    }

    public async Task<TemplateDetailDto> PublishAsync(string id, CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        TemplateDraftGuard.EnsureDraft(existing);

        var payload = new Dictionary<string, object?>
        {
            ["status"] = TemplateStatus.Published,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        return ToDetail(updated);
    }

    public async Task<TemplateDetailDto> UnpublishAsync(string id, CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        if (!string.Equals(existing.status, TemplateStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "TEMPLATE_NOT_PUBLISHED",
                "Template is not published.",
                "Yalnızca üretimde aktif şablonlar taslağa alınabilir.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["status"] = TemplateStatus.Draft,
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        return ToDetail(updated);
    }

    public async Task<TemplateDetailDto> CreateFromSourceAsync(
        CreateTemplateFromSourceRequest request,
        CancellationToken ct = default)
    {
        var sourceId = request.SourceResourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw DocumentException.Validation(
                "SOURCE_REQUIRED",
                "sourceResourceId is required.",
                "Kaynak dosya zorunludur.");
        }

        var resource = await _resources.GetByIdAsync(sourceId, ct);
        if (!string.Equals(resource.Type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_FILE",
                "Source must be a file resource.",
                "Kaynak bir dosya olmalıdır.");
        }

        if (!DocxStructureParser.IsDocxExtension(resource.Extension))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_DOCX",
                "Only DOCX sources are supported in this phase.",
                "Bu aşamada yalnızca DOCX kaynak desteklenir.");
        }

        if (string.IsNullOrWhiteSpace(resource.FilePath))
        {
            throw DocumentException.Validation(
                "SOURCE_FILE_MISSING",
                "Source file path is missing.",
                "Kaynak dosya yolu bulunamadı.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileNameWithoutExtension(resource.FileName ?? resource.Name) + " Şablonu";

        var now = DateTime.UtcNow;
        var payload = BuildCreatePayload(
            name,
            code: null,
            request.Description,
            categoryId: null,
            sourceResourceId: sourceId,
            sourceStoragePath: resource.FilePath,
            sourceFileName: resource.FileName ?? resource.Name,
            creationMode: TemplateCreationMode.FromTemplate,
            referenceFile: null,
            _ctx.Username,
            now);

        var created = await _dg.CreateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            payload,
            Token,
            ct);

        return ToDetail(EnsureCreated(created));
    }

    public async Task<TemplateDetailDto> CreateFromReferenceAsync(
        CreateTemplateFromReferenceRequest request,
        CancellationToken ct = default)
    {
        var categoryId = request.CategoryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            throw DocumentException.Validation(
                "CATEGORY_REQUIRED",
                "categoryId is required.",
                "Kategori seçimi zorunludur.");
        }

        await EnsureCategoryExistsAsync(categoryId, ct);

        var fileName = request.FileName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw DocumentException.Validation(
                "FILE_NAME_REQUIRED",
                "fileName is required.",
                "Dosya adı zorunludur.");
        }

        if (!DocxStructureParser.IsDocxExtension(Path.GetExtension(fileName)))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_DOCX",
                "Only DOCX sources are supported in this phase.",
                "Bu aşamada yalnızca DOCX kaynak desteklenir.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw DocumentException.Validation(
                "CONTENT_REQUIRED",
                "File content is required.",
                "Dosya içeriği zorunludur.");
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileNameWithoutExtension(fileName) + " Şablonu";

        var referenceFile = new Dictionary<string, object?>
        {
            ["content"] = request.Content,
            ["originalFileName"] = fileName
        };

        var now = DateTime.UtcNow;
        var payload = BuildCreatePayload(
            name,
            code: null,
            request.Description,
            categoryId,
            sourceResourceId: null,
            sourceStoragePath: null,
            sourceFileName: fileName,
            creationMode: TemplateCreationMode.FromReference,
            referenceFile,
            _ctx.Username,
            now);

        var created = await _dg.CreateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            payload,
            Token,
            ct);

        created = EnsureCreated(created);
        var (path, storedName) = DgFileFieldReader.Read(created);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var patch = new Dictionary<string, object?>
            {
                ["sourceStoragePath"] = path,
                ["sourceFileName"] = storedName ?? fileName,
                ["updatedBy"] = _ctx.Username,
                ["updatedAt"] = DateTime.UtcNow
            };
            created = await _dg.UpdateAsync<DmDocumentTemplate>(
                DmDatasets.DocumentTemplates,
                created.__dataId!,
                patch,
                Token,
                ct);
        }

        return ToDetail(created);
    }

    public async Task<TemplateDetailDto> DuplicateAsync(
        string id,
        DuplicateTemplateRequest request,
        CancellationToken ct = default)
    {
        var sourceId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sourceId))
            throw DocumentException.NotFound();

        var source = await LoadTemplateOrThrowAsync(sourceId, ct);

        var categoryId = request.CategoryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            throw DocumentException.Validation(
                "CATEGORY_REQUIRED",
                "categoryId is required.",
                "Kategori seçimi zorunludur.");
        }

        await EnsureCategoryExistsAsync(categoryId, ct);

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw DocumentException.Validation(
                "NAME_REQUIRED",
                "Template name is required.",
                "Belge adı zorunludur.");
        }

        var code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "CODE_REQUIRED",
                "Template code is required.",
                "Belge kodu zorunludur.");
        }

        await EnsureCodeUniqueInCategoryAsync(categoryId, code, excludeTemplateId: null, ct);

        var (path, sourceFileName) = await ResolveStoragePathAsync(source, ct);
        var docxBytes = await _dg.DownloadFileAsync(path, Token, ct);

        var model = CloneModelFromSource(source.modelJson);
        ApplyBrandingOverrides(model, request.Letterhead, request.Footer, request.PageLayout);

        var displayFileName = TemplateFileNameHelper.ResolveDisplayFileName(name, code, sourceFileName);
        var referenceFile = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(docxBytes),
            ["originalFileName"] = displayFileName
        };

        var description = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            description = source.description;

        var now = DateTime.UtcNow;
        var payload = BuildCreatePayloadWithModel(
            name,
            code,
            description,
            categoryId,
            TemplateCreationMode.Duplicate,
            TemplateModelSerializer.Serialize(model),
            referenceFile,
            _ctx.Username,
            now);

        var created = await _dg.CreateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            payload,
            Token,
            ct);

        created = EnsureCreated(created);
        var (storedPath, storedName) = DgFileFieldReader.Read(created);
        if (!string.IsNullOrWhiteSpace(storedPath))
        {
            var patch = new Dictionary<string, object?>
            {
                ["sourceStoragePath"] = storedPath,
                ["sourceFileName"] = storedName ?? displayFileName,
                ["updatedBy"] = _ctx.Username,
                ["updatedAt"] = DateTime.UtcNow
            };
            created = await _dg.UpdateAsync<DmDocumentTemplate>(
                DmDatasets.DocumentTemplates,
                created.__dataId!,
                patch,
                Token,
                ct);
        }

        created = await RefreshBrandingDocxAsync(created, ct);

        return ToDetail(created);
    }

    public async Task<DocxStructureDto> GetSourceStructureAsync(string resourceId, CancellationToken ct = default)
    {
        var id = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var resource = await _resources.GetByIdAsync(id, ct);
        if (!string.Equals(resource.Type, ResourceType.File, StringComparison.OrdinalIgnoreCase)
            || !DocxStructureParser.IsDocxExtension(resource.Extension)
            || string.IsNullOrWhiteSpace(resource.FilePath))
        {
            throw DocumentException.Validation(
                "SOURCE_NOT_DOCX",
                "Resource is not a DOCX file.",
                "Kaynak geçerli bir DOCX dosyası değil.");
        }

        return await ParseStructureAsync(id, resource.FilePath, resource.FileName ?? resource.Name, templateId: null, ct);
    }

    public async Task<DocxStructureDto> GetTemplateSourceStructureAsync(string templateId, CancellationToken ct = default)
    {
        var id = templateId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var template = await LoadTemplateOrThrowAsync(id, ct);
        var (path, fileName) = await ResolveStoragePathAsync(template, ct);
        return await ParseStructureAsync(template.sourceResourceId ?? string.Empty, path, fileName, templateId: id, ct);
    }

    public async Task<TemplateDetailDto> UpdateParametersAsync(
        string id,
        UpdateTemplateParametersRequest request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var existing = await LoadTemplateOrThrowAsync(templateId, ct);
        TemplateDraftGuard.EnsureDraft(existing);
        var parameters = request.Parameters ?? Array.Empty<TemplateParameterDto>();

        ValidateParameters(parameters);

        var existingModel = TemplateModelSerializer.Parse(existing.modelJson);
        existingModel.SchemaVersion = TemplateModelSerializer.CurrentSchemaVersion;
        if (request.PrimaryContextType is not null)
            existingModel.PrimaryContextType = string.IsNullOrWhiteSpace(request.PrimaryContextType)
                ? null
                : request.PrimaryContextType.Trim();
        if (request.GenerationProfile is not null)
            existingModel.GenerationProfile = string.IsNullOrWhiteSpace(request.GenerationProfile)
                ? null
                : request.GenerationProfile.Trim();
        existingModel.Parameters = parameters.Select(TemplateParameterMapper.ToModel).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["categoryId"] = existing.categoryId,
            ["name"] = existing.name,
            ["code"] = existing.code,
            ["description"] = existing.description,
            ["sourceResourceId"] = existing.sourceResourceId,
            ["sourceStoragePath"] = existing.sourceStoragePath,
            ["sourceFileName"] = existing.sourceFileName,
            ["creationMode"] = existing.creationMode ?? TemplateCreationMode.FromReference,
            ["status"] = existing.status ?? TemplateStatus.Draft,
            ["modelJson"] = TemplateModelSerializer.Serialize(existingModel),
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            Token,
            ct);

        return ToDetail(updated);
    }

    public async Task<byte[]> RenderTemplatePdfAsync(
        string id,
        RenderTemplatePdfRequest? request,
        CancellationToken ct = default)
    {
        var templateId = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateId))
            throw DocumentException.NotFound();

        var template = await LoadTemplateOrThrowAsync(templateId, ct);
        var detail = ToDetail(template);
        var (path, _) = await ResolveStoragePathAsync(template, ct);
        var docxBytes = await _dg.DownloadFileAsync(path, Token, ct);

        using var ms = new MemoryStream(docxBytes, writable: false);
        var scan = DocxPlaceholderScanner.Scan(ms);
        var mergeValues = BuildMergeValues(
            detail.Parameters,
            scan.Placeholders.Select(p => p.Key).ToList(),
            request?.Values,
            request?.PreserveMissingPlaceholders ?? false);

        return await _render.MergeAndConvertToPdfAsync(docxBytes, mergeValues, ct);
    }

    private static Dictionary<string, string> BuildMergeValues(
        IReadOnlyList<TemplateParameterDto> parameters,
        IReadOnlyList<string> placeholderKeys,
        Dictionary<string, string>? overrides,
        bool preserveMissingPlaceholders = false)
    {
        if (preserveMissingPlaceholders)
        {
            var previewMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (overrides is null)
                return previewMap;

            foreach (var kv in overrides)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    previewMap[kv.Key] = kv.Value.Trim();
            }

            return previewMap;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in placeholderKeys)
        {
            var param = parameters.FirstOrDefault(p =>
                string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (overrides != null && overrides.TryGetValue(key, out var overrideValue))
                map[key] = overrideValue ?? string.Empty;
            else if (param != null)
                map[key] = param.Label ?? param.Key;
            else
                map[key] = key;
        }

        foreach (var param in parameters)
        {
            if (!map.ContainsKey(param.Key))
                map[param.Key] = overrides != null && overrides.TryGetValue(param.Key, out var v)
                    ? v ?? string.Empty
                    : param.Label ?? param.Key;
        }

        if (overrides == null)
            return map;

        foreach (var kv in overrides)
            map[kv.Key] = kv.Value ?? string.Empty;

        return map;
    }

    private async Task EnsureCategoryExistsAsync(string categoryId, CancellationToken ct)
    {
        var cat = await _dg.GetByIdAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, categoryId, Token, ct);
        if (cat is null || cat.__dataId is null)
            throw DocumentException.NotFound("Kategori bulunamadı.");
    }

    private async Task EnsureCodeUniqueInCategoryAsync(
        string categoryId,
        string code,
        string? excludeTemplateId,
        CancellationToken ct)
    {
        var match = new Dictionary<string, object?>
        {
            ["categoryId"] = categoryId,
            ["code"] = code
        };

        var page = await _dg.QueryPageAsync(
            DmDatasets.DocumentTemplates,
            match,
            "limit=5",
            Token,
            ct);

        var duplicate = page.Items.FirstOrDefault(row =>
        {
            if (row.TryGetValue("__dataId", out var idObj) && idObj is not null)
            {
                var rowId = idObj.ToString();
                if (!string.IsNullOrWhiteSpace(excludeTemplateId)
                    && string.Equals(rowId, excludeTemplateId, StringComparison.Ordinal))
                    return false;
            }

            return true;
        });

        if (duplicate is not null)
        {
            throw DocumentException.Validation(
                "CODE_DUPLICATE",
                $"Duplicate template code in category: {code}",
                $"Bu kategoride aynı kod zaten var: {code}");
        }
    }

    private static Dictionary<string, object?> BuildCreatePayload(
        string name,
        string? code,
        string? description,
        string? categoryId,
        string? sourceResourceId,
        string? sourceStoragePath,
        string? sourceFileName,
        string creationMode,
        object? referenceFile,
        string? createdBy,
        DateTime now)
    {
        var emptyModel = TemplateModelSerializer.Serialize(TemplateModelSerializer.NewEmpty());

        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["creationMode"] = creationMode,
            ["status"] = "draft",
            ["modelJson"] = emptyModel,
            ["createdBy"] = createdBy,
            ["createdAt"] = now,
            ["updatedBy"] = createdBy,
            ["updatedAt"] = now
        };

        if (!string.IsNullOrWhiteSpace(description))
            payload["description"] = description.Trim();
        if (!string.IsNullOrWhiteSpace(code))
            payload["code"] = code.Trim();
        if (!string.IsNullOrWhiteSpace(categoryId))
            payload["categoryId"] = categoryId;
        if (!string.IsNullOrWhiteSpace(sourceResourceId))
            payload["sourceResourceId"] = sourceResourceId;
        if (!string.IsNullOrWhiteSpace(sourceStoragePath))
            payload["sourceStoragePath"] = sourceStoragePath;
        if (!string.IsNullOrWhiteSpace(sourceFileName))
            payload["sourceFileName"] = sourceFileName;
        if (referenceFile is not null)
            payload["referenceFile"] = referenceFile;

        return payload;
    }

    private static Dictionary<string, object?> BuildCreatePayloadWithModel(
        string name,
        string code,
        string? description,
        string categoryId,
        string creationMode,
        string modelJson,
        object referenceFile,
        string? createdBy,
        DateTime now)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["code"] = code.Trim(),
            ["categoryId"] = categoryId,
            ["creationMode"] = creationMode,
            ["status"] = TemplateStatus.Draft,
            ["modelJson"] = modelJson,
            ["sourceFileName"] = TemplateFileNameHelper.ResolveDisplayFileName(name, code, null),
            ["referenceFile"] = referenceFile,
            ["createdBy"] = createdBy,
            ["createdAt"] = now,
            ["updatedBy"] = createdBy,
            ["updatedAt"] = now
        };

        if (!string.IsNullOrWhiteSpace(description))
            payload["description"] = description.Trim();

        return payload;
    }

    private static TemplateModelDocument CloneModelFromSource(string? modelJson)
    {
        var source = TemplateModelSerializer.Parse(modelJson);
        var clone = TemplateModelSerializer.Parse(TemplateModelSerializer.Serialize(source));
        clone.SchemaVersion = TemplateModelSerializer.CurrentSchemaVersion;
        return clone;
    }

    private static void ApplyBrandingOverrides(
        TemplateModelDocument model,
        TemplateLetterheadDto? letterheadDto,
        TemplateFooterDto? footerDto,
        TemplatePageLayoutDto? pageLayoutDto)
    {
        if (letterheadDto is not null)
        {
            var letterhead = TemplateModelSerializer.ToLetterheadModel(letterheadDto)
                             ?? new TemplateLetterheadModel();
            if (letterhead.Enabled)
            {
                model.Letterhead = letterhead;
                model.Parameters = TemplateModelSerializer.EnsureLetterheadParameters(
                    letterhead,
                    model.Parameters);
            }
            else
            {
                model.Letterhead = new TemplateLetterheadModel { Enabled = false };
                model.Parameters = TemplateModelSerializer.RemoveSystemLetterheadParameters(model.Parameters);
            }
        }

        if (footerDto is not null)
        {
            var footer = TemplateModelSerializer.ToFooterModel(footerDto)
                         ?? new TemplateFooterModel();
            model.Footer = footer.Enabled ? footer : new TemplateFooterModel { Enabled = false };
        }

        if (pageLayoutDto is not null)
        {
            model.PageLayout = TemplateModelSerializer.ToPageLayoutModel(pageLayoutDto)
                               ?? TemplatePageLayoutModel.CreateDefault();
        }
    }

    private async Task<(string Path, string? FileName)> ResolveStoragePathAsync(DmDocumentTemplate template, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(template.sourceStoragePath))
            return (template.sourceStoragePath, template.sourceFileName);

        var (path, name) = DgFileFieldReader.Read(template);
        if (!string.IsNullOrWhiteSpace(path))
            return (path!, name);

        var sourceId = template.sourceResourceId?.Trim();
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw DocumentException.Validation(
                "SOURCE_FILE_MISSING",
                "Template source file is missing.",
                "Şablon kaynak dosyası bulunamadı.");
        }

        var resource = await _resources.GetByIdAsync(sourceId, ct);
        if (string.IsNullOrWhiteSpace(resource.FilePath))
        {
            throw DocumentException.Validation(
                "SOURCE_FILE_MISSING",
                "Source file path is missing.",
                "Kaynak dosya yolu bulunamadı.");
        }

        return (resource.FilePath, resource.FileName ?? resource.Name);
    }

    private async Task<DocxStructureDto> ParseStructureAsync(
        string resourceId,
        string filePath,
        string? fileName,
        string? templateId,
        CancellationToken ct)
    {
        var bytes = await _dg.DownloadFileAsync(filePath, Token, ct);
        using var ms = new MemoryStream(bytes, writable: false);
        ms.Position = 0;
        var parsed = DocxStructureParser.Parse(ms);
        ms.Position = 0;
        var placeholderScan = DocxPlaceholderScanner.Scan(ms);

        return new DocxStructureDto
        {
            TemplateId = templateId ?? string.Empty,
            ResourceId = resourceId,
            FileName = fileName,
            TableCount = parsed.TableCount,
            Paragraphs = parsed.Paragraphs
                .Select(p => new DocxParagraphDto { Index = p.Index, Text = p.Text })
                .ToList(),
            Placeholders = placeholderScan.Placeholders
                .Select(p => new DocxPlaceholderDto
                {
                    Key = p.Key,
                    Token = p.Token,
                    OccurrenceCount = p.OccurrenceCount
                })
                .ToList(),
            PlaceholderWarnings = placeholderScan.Warnings.ToList()
        };
    }

    private static DmDocumentTemplate EnsureCreated(DmDocumentTemplate created)
    {
        if (created.__dataId is null)
        {
            throw DocumentException.Validation(
                "TEMPLATE_CREATE_FAILED",
                "Template could not be created.",
                "Şablon oluşturulamadı.");
        }

        return created;
    }

    private static void ValidateParameters(IReadOnlyList<TemplateParameterDto> parameters)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            var key = p.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                throw DocumentException.Validation(
                    "PARAM_KEY_REQUIRED",
                    "Parameter key is required.",
                    "Parametre anahtarı zorunludur.");
            }

            if (!keys.Add(key))
            {
                throw DocumentException.Validation(
                    "PARAM_KEY_DUPLICATE",
                    $"Duplicate parameter key: {key}",
                    $"Yinelenen parametre anahtarı: {key}");
            }

            if (string.Equals(p.ValueSourceMode, "incremental", StringComparison.OrdinalIgnoreCase)
                && (p.Incremental is null || string.IsNullOrWhiteSpace(p.Incremental.Format)))
            {
                throw DocumentException.Validation(
                    "INCREMENTAL_FORMAT_REQUIRED",
                    "Incremental parameters require format.",
                    "Otomatik numara parametreleri için format zorunludur.");
            }

            if (string.Equals(p.ValueSourceMode, "generated", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(p.DataType, "datetime", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(p.DataType, "date", StringComparison.OrdinalIgnoreCase))
            {
                throw DocumentException.Validation(
                    "GENERATED_PARAM_INVALID",
                    "Generated parameters must use date/datetime type.",
                    "Üretim zamanı parametreleri date/datetime olmalıdır.");
            }

            if (string.Equals(p.ValueSourceMode, "context", StringComparison.OrdinalIgnoreCase)
                && (p.ContextBinding is null || string.IsNullOrWhiteSpace(p.ContextBinding.Path)))
            {
                throw DocumentException.Validation(
                    "CONTEXT_PATH_REQUIRED",
                    "Context parameters require contextBinding.path.",
                    "Kaynak alanı parametreleri için contextBinding.path zorunludur.");
            }
        }
    }

    private async Task<DmDocumentTemplate> RefreshBrandingDocxAsync(
        DmDocumentTemplate template,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Token))
            return template;

        var model = TemplateModelSerializer.Parse(template.modelJson);
        var letterheadResolve = await _letterheads.ResolveAsync(
            model.DefaultLetterheadId,
            TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            ct);
        var letterheadEnabled = letterheadResolve.Letterhead is { Enabled: true }
            ? TemplateModelSerializer.ToLetterheadModel(letterheadResolve.Letterhead)
            : null;
        var (footerModel, pageLayout) = LetterheadBrandingResolver.Resolve(letterheadResolve, model);
        if (letterheadEnabled is null && footerModel is null && model.PageLayout is null)
            return template;

        var fileName = TemplateFileNameHelper.ResolveDisplayFileName(
            template.name,
            template.code,
            template.sourceFileName);
        var docx = await TemplateDocxUpdater.LoadDocxAsync(_dg, template, Token, ct);
        var letterheadDesignDocx = await TryLoadLetterheadDesignDocxAsync(model, ct);
        var letterheadSettings = !string.IsNullOrWhiteSpace(letterheadResolve.LetterheadId)
            ? letterheadResolve.Settings
            : null;
        var withBranding = await _brandingApplier.ApplyAsync(
            docx,
            template.name ?? string.Empty,
            letterheadEnabled,
            footerModel,
            pageLayout,
            letterheadDesignDocx,
            letterheadSettings,
            Token,
            ct);

        template.modelJson = TemplateModelSerializer.Serialize(model);
        return await TemplateDocxUpdater.ReplaceDocxAsync(
            _dg,
            template,
            template.__dataId!,
            withBranding,
            fileName,
            _ctx.Username,
            Token!,
            ct);
    }

    private static TemplateModelDocument ParseModel(string? modelJson) =>
        TemplateModelSerializer.Parse(modelJson);

    private async Task<TemplateLetterheadModel?> ResolveBrandingLetterheadAsync(
        TemplateModelDocument model,
        CancellationToken ct)
    {
        var resolved = await _letterheads.ResolveAsync(
            model.DefaultLetterheadId,
            TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            ct);
        if (resolved.Letterhead is not { Enabled: true })
            return null;

        return TemplateModelSerializer.ToLetterheadModel(resolved.Letterhead);
    }

    private async Task<byte[]?> TryLoadLetterheadDesignDocxAsync(
        TemplateModelDocument model,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.DefaultLetterheadId))
            return null;

        var letterhead = await _letterheads.TryGetByIdAsync(model.DefaultLetterheadId, ct);
        if (letterhead is not { HasDesign: true }
            || string.IsNullOrWhiteSpace(letterhead.DesignStoragePath))
            return null;

        return await _dg.DownloadFileAsync(letterhead.DesignStoragePath, Token, ct);
    }

    private async Task<DmDocumentTemplate> LoadTemplateOrThrowAsync(string id, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<DmDocumentTemplate>(DmDatasets.DocumentTemplates, id, Token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Şablon bulunamadı.");
        return row;
    }

    private static TemplateSummaryDto MapSummaryRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        var entity = JsonSerializer.Deserialize<DmDocumentTemplate>(json, JsonOptions) ?? new DmDocumentTemplate();
        return ToSummary(entity);
    }

    private static TemplateSummaryDto ToSummary(DmDocumentTemplate row)
    {
        var (path, fileName) = DgFileFieldReader.Read(row);
        var model = ParseModel(row.modelJson);
        return new()
        {
            Id = row.__dataId ?? string.Empty,
            CategoryId = row.categoryId,
            Name = row.name ?? string.Empty,
            Code = row.code,
            Description = row.description,
            SourceResourceId = row.sourceResourceId,
            SourceStoragePath = row.sourceStoragePath ?? path,
            SourceFileName = row.sourceFileName ?? fileName,
            CreationMode = row.creationMode ?? TemplateCreationMode.FromReference,
            Status = row.status ?? TemplateStatus.Draft,
            ParameterCount = model.Parameters.Count,
            PrimaryContextType = model.PrimaryContextType,
            GenerationProfile = model.GenerationProfile,
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };
    }

    private static TemplateDetailDto ToDetail(DmDocumentTemplate row)
    {
        var model = ParseModel(row.modelJson);
        var summary = ToSummary(row);
        return new TemplateDetailDto
        {
            Id = summary.Id,
            CategoryId = summary.CategoryId,
            Name = summary.Name,
            Code = summary.Code,
            Description = summary.Description,
            SourceResourceId = summary.SourceResourceId,
            SourceStoragePath = summary.SourceStoragePath,
            SourceFileName = summary.SourceFileName,
            CreationMode = summary.CreationMode,
            Status = summary.Status,
            ParameterCount = model.Parameters.Count,
            CreatedBy = summary.CreatedBy,
            CreatedAt = summary.CreatedAt,
            UpdatedAt = summary.UpdatedAt,
            SchemaVersion = model.SchemaVersion,
            PrimaryContextType = model.PrimaryContextType,
            GenerationProfile = model.GenerationProfile,
            DefaultLetterheadId = model.DefaultLetterheadId,
            Letterhead = TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            Footer = TemplateModelSerializer.ToFooterDto(model.Footer),
            PageLayout = TemplateModelSerializer.ToPageLayoutDto(model.PageLayout),
            Parameters = model.Parameters.Select(TemplateParameterMapper.ToDto).ToList()
        };
    }
}
