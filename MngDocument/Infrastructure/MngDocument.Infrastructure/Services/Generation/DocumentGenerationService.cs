using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentGenerationService : IDocumentGenerationService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IResourceService _resources;
    private readonly IRequestContext _ctx;
    private readonly DocumentContextLoader _contextLoader;
    private readonly DocumentParameterResolver _parameterResolver;
    private readonly DocumentGenerationSettings _generationSettings;
    private readonly ITemplateBrandingApplier _brandingApplier;
    private readonly ILetterheadService _letterheads;
    private readonly LetterheadHeaderValueEnricher _headerEnricher;

    public DocumentGenerationService(
        IMngDataGatewayClient dg,
        IResourceService resources,
        IRequestContext ctx,
        DocumentContextLoader contextLoader,
        DocumentParameterResolver parameterResolver,
        IOptions<MngDocumentSettings> settings,
        ITemplateBrandingApplier brandingApplier,
        ILetterheadService letterheads,
        LetterheadHeaderValueEnricher headerEnricher)
    {
        _dg = dg;
        _resources = resources;
        _ctx = ctx;
        _contextLoader = contextLoader;
        _parameterResolver = parameterResolver;
        _generationSettings = settings.Value.DocumentGeneration ?? new DocumentGenerationSettings();
        _brandingApplier = brandingApplier;
        _letterheads = letterheads;
        _headerEnricher = headerEnricher;
    }

    private string? Token => _ctx.BearerToken;

    public IReadOnlyList<DocumentContextTypeDto> ListContextTypes() =>
        DocumentContextCatalog.All()
            .Select(def => new DocumentContextTypeDto
            {
                Type = def.Type,
                DisplayName = def.DisplayName,
                RootDataset = def.RootDataset,
                Fields = def.Fields
            })
            .ToList();

    public DocumentContextTypeDto? GetContextType(string type)
    {
        var def = DocumentContextCatalog.TryGet(type);
        return def is null
            ? null
            : new DocumentContextTypeDto
            {
                Type = def.Type,
                DisplayName = def.DisplayName,
                RootDataset = def.RootDataset,
                Fields = def.Fields
            };
    }

    public async Task<DocumentGenerationStatusDto> GetStatusAsync(
        string profileCode,
        string contextId,
        CancellationToken ct = default)
    {
        var profile = ResolveProfile(profileCode);
        var idempotency = profile.Idempotency;
        if (idempotency is null || string.IsNullOrWhiteSpace(idempotency.GuardField))
        {
            return new DocumentGenerationStatusDto
            {
                ProfileCode = profile.Code,
                ContextType = profile.ContextType,
                ContextId = contextId.Trim(),
                Generated = false
            };
        }

        var row = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            idempotency.Dataset,
            contextId.Trim(),
            Token,
            ct);

        if (row is null)
            throw DocumentException.NotFound();

        var resourceId = ReadString(row, idempotency.GuardField);
        var generated = !string.IsNullOrWhiteSpace(resourceId);

        return new DocumentGenerationStatusDto
        {
            ProfileCode = profile.Code,
            ContextType = profile.ContextType,
            ContextId = contextId.Trim(),
            Generated = generated,
            ResourceId = resourceId,
            DocNo = ReadString(row, "cocDocNo"),
            GeneratedAt = ReadDateTime(row, "cocGeneratedAt")
        };
    }

    public async Task<DocumentGenerationPreviewDto> PreviewAsync(
        string profileCode,
        string contextId,
        CancellationToken ct = default)
    {
        var profile = ResolveProfile(profileCode);
        var templateCode = profile.TemplateCode;
        var (templateRow, model, values) = await BuildResolvedValuesAsync(
            profile,
            contextId.Trim(),
            templateCode,
            null,
            ct);

        var letterheadResolve = await _letterheads.ResolveAsync(
            model.DefaultLetterheadId,
            TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            ct);
        var letterheadEntry = await TryLoadLetterheadEntryAsync(letterheadResolve, ct);
        await _headerEnricher.EnrichAsync(
            values,
            model,
            letterheadEntry,
            templateRow.name,
            _ctx,
            allocateCounters: false,
            Token,
            ct);

        var docxBytes = await LoadTemplateDocxAsync(templateRow, ct);
        var placeholderAnalysis = AnalyzePlaceholders(docxBytes, model, values);

        var missing = model.Parameters
            .Where(p => !values.TryGetValue(p.Key, out var v) || string.IsNullOrWhiteSpace(v))
            .Select(p => p.Key)
            .ToList();

        return new DocumentGenerationPreviewDto
        {
            ProfileCode = profile.Code,
            ContextType = profile.ContextType,
            ContextId = contextId.Trim(),
            Values = values,
            MissingKeys = missing,
            UndefinedParameterKeys = placeholderAnalysis.UndefinedParameterKeys,
            UnresolvedParameterKeys = placeholderAnalysis.UnresolvedParameterKeys
        };
    }

    public async Task<GenerateDocumentResultDto> GenerateAsync(
        GenerateDocumentRequest request,
        CancellationToken ct = default)
    {
        var profile = ResolveProfile(request.ProfileCode);
        var contextId = request.Context.Id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contextId))
        {
            throw DocumentException.Validation(
                "CONTEXT_ID_REQUIRED",
                "Context id is required.",
                "Kaynak kayıt id zorunludur.");
        }

        if (!string.Equals(profile.ContextType, request.Context.Type?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "CONTEXT_TYPE_MISMATCH",
                "Context type does not match profile.",
                "Bağlam tipi profil ile uyuşmuyor.");
        }

        await EnsureNotGeneratedAsync(profile, contextId, ct);

        var templateCode = ResolveTemplateCode(profile, request.TemplateCode);
        var (templateRow, model, values) = await BuildResolvedValuesAsync(
            profile,
            contextId,
            templateCode,
            request.Overrides,
            ct);

        ValidateTemplateForProfile(profile, templateRow, model);

        var letterheadResolve = await _letterheads.ResolveAsync(
            model.DefaultLetterheadId,
            TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            ct);
        var letterheadEntry = await TryLoadLetterheadEntryAsync(letterheadResolve, ct);
        await _headerEnricher.EnrichAsync(
            values,
            model,
            letterheadEntry,
            templateRow.name,
            _ctx,
            allocateCounters: true,
            Token,
            ct);

        var docxBytes = await LoadTemplateDocxAsync(templateRow, ct);
        var placeholderAnalysis = AnalyzePlaceholders(docxBytes, model, values);

        var letterheadModel = letterheadResolve.Letterhead is { Enabled: true }
            ? TemplateModelSerializer.ToLetterheadModel(letterheadResolve.Letterhead)
            : null;
        var (footerModel, pageLayout) = LetterheadBrandingResolver.Resolve(letterheadResolve, model);

        var branded = docxBytes;
        if (letterheadModel is not null || footerModel is not null || !string.IsNullOrWhiteSpace(letterheadResolve.LetterheadId))
        {
            var letterheadDesignDocx = await TryLoadLetterheadDesignDocxAsync(letterheadEntry, ct);
            var letterheadSettings = !string.IsNullOrWhiteSpace(letterheadResolve.LetterheadId)
                ? letterheadResolve.Settings
                : null;
            branded = await _brandingApplier.ApplyAsync(
                docxBytes,
                templateRow.name ?? string.Empty,
                letterheadModel,
                footerModel,
                pageLayout,
                letterheadDesignDocx,
                letterheadSettings,
                Token,
                ct);
        }

        var merged = DocxPlaceholderMerger.Merge(
            branded,
            values,
            placeholderAnalysis.PreservePlaceholderKeys);
        var remainingPlaceholders = DocumentPlaceholderAnalysis.ScanRemainingPlaceholderKeys(merged);

        var fileName = ApplyPattern(profile.FileNamePattern, values, contextId);
        var folderSegments = profile.OutputFolderPath
            .Select(segment => ApplyPattern(segment, values, contextId))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var parentFolderId = await EnsureFolderPathAsync(folderSegments, ct);
        var businessDocNo = ResolveBusinessDocNo(values);
        var saved = await _resources.CreateFileResourceAsync(new CreateFileResourceRequest
        {
            ParentId = parentFolderId,
            Name = fileName,
            OriginalFileName = fileName,
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Extension = ".docx",
            Size = merged.Length,
            Content = Convert.ToBase64String(merged),
            Origin = "system",
            TemplateId = templateRow.__dataId,
            TemplateCode = templateRow.code ?? templateCode,
            GenerationProfile = profile.Code,
            LetterheadId = letterheadResolve.LetterheadId,
            DocumentNo = businessDocNo
        }, ct);

        await WritebackAsync(profile, contextId, templateRow, templateCode, values, saved.Id, ct);

        var generatedAt = DateTime.UtcNow;

        return new GenerateDocumentResultDto
        {
            ProfileCode = profile.Code,
            ContextType = profile.ContextType,
            ContextId = contextId,
            TemplateId = templateRow.__dataId ?? string.Empty,
            TemplateCode = templateRow.code ?? profile.TemplateCode,
            LetterheadId = letterheadResolve.LetterheadId,
            LetterheadCode = letterheadResolve.LetterheadCode,
            LetterheadName = letterheadResolve.LetterheadName,
            DocNo = businessDocNo,
            ResourceId = saved.Id,
            FileName = fileName,
            FolderPath = folderSegments,
            GeneratedAt = generatedAt,
            ResolvedValues = values,
            UndefinedParameterKeys = placeholderAnalysis.UndefinedParameterKeys,
            UnresolvedParameterKeys = placeholderAnalysis.UnresolvedParameterKeys,
            RemainingPlaceholderKeys = remainingPlaceholders
        };
    }

    private static DocumentPlaceholderAnalysis.Result AnalyzePlaceholders(
        byte[] docxBytes,
        TemplateModelDocument model,
        IReadOnlyDictionary<string, string> values)
    {
        using var stream = new MemoryStream(docxBytes, writable: false);
        var scan = DocxPlaceholderScanner.Scan(stream);
        return DocumentPlaceholderAnalysis.Analyze(scan, model, values);
    }

    private async Task EnsureNotGeneratedAsync(
        DocumentGenerationProfileSettings profile,
        string contextId,
        CancellationToken ct)
    {
        var guard = profile.Idempotency?.GuardField;
        if (string.IsNullOrWhiteSpace(guard))
            return;

        var status = await GetStatusAsync(profile.Code, contextId, ct);
        if (!status.Generated)
            return;

        throw DocumentException.Conflict(
            "DOCUMENT_ALREADY_GENERATED",
            "A document was already generated for this context.",
            "Bu kayıt için belge zaten üretilmiş.");
    }

    private async Task<(DmDocumentTemplate Template, TemplateModelDocument Model, Dictionary<string, string> Values)> BuildResolvedValuesAsync(
        DocumentGenerationProfileSettings profile,
        string contextId,
        string templateCode,
        Dictionary<string, string>? overrides,
        CancellationToken ct)
    {
        var contextDef = DocumentContextCatalog.GetRequired(profile.ContextType);
        var contextTree = await _contextLoader.LoadAsync(contextDef, contextId, Token, ct);
        var templateRow = await LoadTemplateByCodeAsync(templateCode, ct);
        var model = TemplateModelSerializer.Parse(templateRow.modelJson);
        var values = await _parameterResolver.ResolveAsync(
            model,
            contextTree,
            profile.Defaults,
            overrides,
            Token,
            ct);

        EnrichPatternTokens(values, contextTree);

        return (templateRow, model, values);
    }

    private async Task<LetterheadDto?> TryLoadLetterheadEntryAsync(
        LetterheadResolveResult resolve,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(resolve.LetterheadId))
            return null;

        return await _letterheads.TryGetByIdAsync(resolve.LetterheadId, ct);
    }

    private async Task<byte[]?> TryLoadLetterheadDesignDocxAsync(LetterheadDto? letterhead, CancellationToken ct)
    {
        if (letterhead is not { HasDesign: true }
            || string.IsNullOrWhiteSpace(letterhead.DesignStoragePath))
            return null;

        return await _dg.DownloadFileAsync(letterhead.DesignStoragePath, Token, ct);
    }

    private static string ResolveTemplateCode(
        DocumentGenerationProfileSettings profile,
        string? requestTemplateCode)
    {
        var code = string.IsNullOrWhiteSpace(requestTemplateCode)
            ? profile.TemplateCode
            : requestTemplateCode.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "TEMPLATE_CODE_REQUIRED",
                "Template code is required.",
                "Şablon kodu zorunludur.");
        }

        return code;
    }

    private static void ValidateTemplateForProfile(
        DocumentGenerationProfileSettings profile,
        DmDocumentTemplate templateRow,
        TemplateModelDocument model)
    {
        var status = templateRow.status?.Trim() ?? string.Empty;
        if (!string.Equals(status, TemplateStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "TEMPLATE_NOT_PUBLISHED",
                "Template must be published before document generation.",
                "Belge üretimi için şablon yayımlanmış olmalıdır.");
        }

        if (!string.IsNullOrWhiteSpace(model.PrimaryContextType)
            && !string.Equals(model.PrimaryContextType, profile.ContextType, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "TEMPLATE_CONTEXT_MISMATCH",
                "Template primary context does not match profile.",
                "Şablon birincil bağlamı profil ile uyuşmuyor.");
        }

        if (!string.IsNullOrWhiteSpace(model.GenerationProfile)
            && !string.Equals(model.GenerationProfile, profile.Code, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "TEMPLATE_PROFILE_MISMATCH",
                "Template generation profile does not match request profile.",
                "Şablon üretim profili istek profili ile uyuşmuyor.");
        }
    }

    private static void EnrichPatternTokens(Dictionary<string, string> values, JsonObject contextTree)
    {
        void TryAdd(string key, string? path)
        {
            if (values.ContainsKey(key))
                return;
            var v = DocumentContextPathResolver.GetString(contextTree, path);
            if (!string.IsNullOrWhiteSpace(v))
                values[key] = v;
        }

        TryAdd("workPackageNo", "parentPackageId.packageNo");
        TryAdd("lineNo", "lineNo");
    }

    private async Task<DmDocumentTemplate> LoadTemplateByCodeAsync(string templateCode, CancellationToken ct)
    {
        var code = templateCode.Trim();
        var page = await _dg.QueryPageAsync(
            DmDatasets.DocumentTemplates,
            new Dictionary<string, object?> { ["code"] = code },
            "limit=5",
            Token,
            ct);

        var row = page.Items
            .Select(MapTemplateRow)
            .FirstOrDefault(t => string.Equals(t.code, code, StringComparison.OrdinalIgnoreCase));

        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
        {
            throw DocumentException.Validation(
                "TEMPLATE_NOT_FOUND",
                $"Template not found: {code}",
                $"Şablon bulunamadı: {code}");
        }

        return row;
    }

    private async Task<byte[]> LoadTemplateDocxAsync(DmDocumentTemplate template, CancellationToken ct)
    {
        var path = template.sourceStoragePath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            var (resolvedPath, _) = await ResolveStoragePathFallbackAsync(template, ct);
            path = resolvedPath;
        }

        return await _dg.DownloadFileAsync(path!, Token, ct);
    }

    private async Task<(string Path, string? FileName)> ResolveStoragePathFallbackAsync(
        DmDocumentTemplate template,
        CancellationToken ct)
    {
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

    private async Task<string?> EnsureFolderPathAsync(IReadOnlyList<string> segments, CancellationToken ct)
    {
        string? parentId = null;
        foreach (var segment in segments)
        {
            var children = await _resources.GetChildrenAsync(parentId, ct);
            var existing = children.Items.FirstOrDefault(c =>
                string.Equals(c.Type, ResourceType.Folder, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.Name, segment, StringComparison.Ordinal));

            if (existing is not null)
            {
                parentId = existing.Id;
                continue;
            }

            var created = await _resources.CreateFolderAsync(new CreateFolderRequest
            {
                ParentId = parentId,
                Name = segment
            }, ct);

            parentId = created.Id;
        }

        return parentId;
    }

    private async Task WritebackAsync(
        DocumentGenerationProfileSettings profile,
        string contextId,
        DmDocumentTemplate templateRow,
        string templateCode,
        Dictionary<string, string> values,
        string resourceId,
        CancellationToken ct)
    {
        var idempotency = profile.Idempotency;
        if (idempotency is null || idempotency.WritebackFields.Count == 0)
            return;

        var payload = new Dictionary<string, object?>();
        foreach (var field in idempotency.WritebackFields)
        {
            switch (field.ToLowerInvariant())
            {
                case "cocdiresourceid":
                    payload["cocDiResourceId"] = resourceId;
                    break;
                case "cocdocno":
                    if (ResolveBusinessDocNo(values) is { } cocDocNo)
                        payload["cocDocNo"] = cocDocNo;
                    break;
                case "cocgeneratedat":
                    payload["cocGeneratedAt"] = DateTime.UtcNow;
                    break;
                case "coctemplatecode":
                    payload["cocTemplateCode"] = templateRow.code ?? templateCode;
                    break;
                case "coctemplatename":
                    payload["cocTemplateName"] = templateRow.name ?? string.Empty;
                    break;
                case "activitydiresourceid":
                    payload["activityDiResourceId"] = resourceId;
                    break;
                case "activitydocno":
                    if (ResolveBusinessDocNo(values) is { } activityDocNo)
                        payload["activityDocNo"] = activityDocNo;
                    break;
                case "activitygeneratedat":
                    payload["activityGeneratedAt"] = DateTime.UtcNow;
                    break;
                case "activitytemplatecode":
                    payload["activityTemplateCode"] = templateRow.code ?? templateCode;
                    break;
                case "activitytemplatename":
                    payload["activityTemplateName"] = templateRow.name ?? string.Empty;
                    break;
                default:
                    payload[field] = field.Equals(idempotency.GuardField, StringComparison.OrdinalIgnoreCase)
                        ? resourceId
                        : values.GetValueOrDefault(field);
                    break;
            }
        }

        await _dg.UpdateAsync<Dictionary<string, object?>>(
            idempotency.Dataset,
            contextId,
            payload,
            Token,
            ct);
    }

    private static string? ResolveBusinessDocNo(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue(LetterheadConstants.PoDocNoKey, out var poDocNo)
            && !string.IsNullOrWhiteSpace(poDocNo))
            return poDocNo;

        if (values.TryGetValue(LetterheadConstants.DocNoKey, out var docNo)
            && !string.IsNullOrWhiteSpace(docNo))
            return docNo;

        return null;
    }

    private DocumentGenerationProfileSettings ResolveProfile(string? profileCode)
    {
        var code = profileCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "PROFILE_CODE_REQUIRED",
                "Profile code is required.",
                "Üretim profili kodu zorunludur.");
        }

        var profile = _generationSettings.Profiles.FirstOrDefault(p =>
            string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            throw DocumentException.Validation(
                "PROFILE_NOT_FOUND",
                $"Generation profile not found: {code}",
                $"Üretim profili bulunamadı: {code}");
        }

        return profile;
    }

    private static string ApplyPattern(string pattern, IReadOnlyDictionary<string, string> values, string contextId)
    {
        var result = pattern;
        foreach (var kv in values)
            result = result.Replace($"{{{kv.Key}}}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        result = result.Replace("{contextId}", contextId, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static DmDocumentTemplate MapTemplateRow(Dictionary<string, object?> row)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(row);
        return System.Text.Json.JsonSerializer.Deserialize<DmDocumentTemplate>(json)
               ?? new DmDocumentTemplate();
    }

    private static string? ReadString(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var raw) && raw is not null
            ? Convert.ToString(raw, CultureInfo.InvariantCulture)?.Trim()
            : null;

    private static DateTime? ReadDateTime(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
            return null;

        if (raw is DateTime dt)
            return dt;

        return DateTime.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : null;
    }
}
