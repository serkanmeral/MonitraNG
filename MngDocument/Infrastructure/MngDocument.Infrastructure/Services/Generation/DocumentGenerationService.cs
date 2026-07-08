using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.CoverPages;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Generation;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Services;

namespace MngDocument.Infrastructure.Services.Generation;

public sealed class DocumentGenerationService : IDocumentGenerationService
{
    internal const string ManualProfileCode = "di.manual";

    private readonly IMngDataGatewayClient _dg;
    private readonly IResourceService _resources;
    private readonly IRequestContext _ctx;
    private readonly DocumentContextLoader _contextLoader;
    private readonly DocumentContextCatalogProvider _contextCatalog;
    private readonly DocumentProducerCatalogProvider _producerCatalog;
    private readonly DocumentDataSourceCatalogProvider _dataSourceCatalog;
    private readonly DocumentParameterResolver _parameterResolver;
    private readonly ITemplateBrandingApplier _brandingApplier;
    private readonly ILetterheadService _letterheads;
    private readonly ICoverPageService _coverPages;
    private readonly LetterheadHeaderValueEnricher _headerEnricher;
    private readonly PackageDashboardMetricsEnricher _packageDashboardEnricher;
    private readonly IDomainLogoProvider _logoProvider;
    private readonly ITemplateEditorService _templateEditor;

    public DocumentGenerationService(
        IMngDataGatewayClient dg,
        IResourceService resources,
        IRequestContext ctx,
        DocumentContextLoader contextLoader,
        DocumentContextCatalogProvider contextCatalog,
        DocumentProducerCatalogProvider producerCatalog,
        DocumentDataSourceCatalogProvider dataSourceCatalog,
        DocumentParameterResolver parameterResolver,
        IOptions<MngDocumentSettings> settings,
        ITemplateBrandingApplier brandingApplier,
        ILetterheadService letterheads,
        ICoverPageService coverPages,
        LetterheadHeaderValueEnricher headerEnricher,
        PackageDashboardMetricsEnricher packageDashboardEnricher,
        IDomainLogoProvider logoProvider,
        ITemplateEditorService templateEditor)
    {
        _dg = dg;
        _resources = resources;
        _ctx = ctx;
        _contextLoader = contextLoader;
        _contextCatalog = contextCatalog;
        _producerCatalog = producerCatalog;
        _dataSourceCatalog = dataSourceCatalog;
        _parameterResolver = parameterResolver;
        _brandingApplier = brandingApplier;
        _letterheads = letterheads;
        _coverPages = coverPages;
        _headerEnricher = headerEnricher;
        _packageDashboardEnricher = packageDashboardEnricher;
        _logoProvider = logoProvider;
        _templateEditor = templateEditor;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<IReadOnlyList<DocumentContextTypeDto>> ListContextTypesAsync(CancellationToken ct = default)
    {
        var defs = await _contextCatalog.AllAsync(ct);
        return defs.Select(MapContextType).ToList();
    }

    public async Task<DocumentContextTypeDto?> GetContextTypeAsync(string type, CancellationToken ct = default)
    {
        var def = await _contextCatalog.TryGetAsync(type, ct);
        return def is null ? null : MapContextType(def);
    }

    private static DocumentContextTypeDto MapContextType(DocumentContextTypeDefinition def) =>
        new()
        {
            Type = def.Type,
            DisplayName = def.DisplayName,
            RootDataset = def.RootDataset,
            Fields = def.Fields
        };

    public async Task<DocumentProducerDetailDto?> GetProducerAsync(string code, CancellationToken ct = default)
    {
        var profile = await _producerCatalog.TryGetAsync(code, ct);
        return profile is null ? null : MapProducerDetail(profile);
    }

    public async Task<IReadOnlyList<DocumentDataSourceSummaryDto>> ListDataSourcesAsync(CancellationToken ct = default)
    {
        var entries = await _dataSourceCatalog.ListEntriesAsync(ct);
        return entries.Select(MapDataSourceSummary).ToList();
    }

    public async Task<DocumentDataSourceDetailDto?> GetDataSourceAsync(string code, CancellationToken ct = default)
    {
        var entry = await _dataSourceCatalog.TryGetEntryAsync(code, ct);
        return entry is null ? null : MapDataSourceDetail(entry);
    }

    public async Task<IReadOnlyList<DocumentProducerDto>> ListProducersAsync(CancellationToken ct = default)
    {
        var items = await _producerCatalog.AllAsync(ct);
        return items.Select(p => new DocumentProducerDto
        {
            Code = p.Code,
            DisplayName = p.DisplayName,
            ContextType = p.ContextType,
            TemplateCode = p.TemplateCode
        }).ToList();
    }

    private static DocumentProducerDto MapProducer(DocumentGenerationProfileSettings profile) =>
        new()
        {
            Code = profile.Code,
            DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Code : profile.DisplayName,
            ContextType = profile.ContextType,
            TemplateCode = profile.TemplateCode
        };

    private static DocumentProducerDetailDto MapProducerDetail(DocumentGenerationProfileSettings profile) =>
        new()
        {
            Code = profile.Code,
            DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Code : profile.DisplayName,
            ContextType = profile.ContextType,
            TemplateCode = profile.TemplateCode,
            OutputFormat = string.IsNullOrWhiteSpace(profile.OutputFormat) ? "docx" : profile.OutputFormat.Trim(),
            OutputFolderPath = profile.OutputFolderPath?.ToList() ?? new List<string>(),
            FileNamePattern = profile.FileNamePattern ?? string.Empty,
            IdempotencyDataset = profile.Idempotency?.Dataset,
            IdempotencyGuardField = profile.Idempotency?.GuardField,
            WritebackFields = profile.Idempotency?.WritebackFields?.ToList() ?? new List<string>()
        };

    private static DocumentDataSourceSummaryDto MapDataSourceSummary(DataSourceCatalogEntry entry)
    {
        var def = entry.Definition;
        return new DocumentDataSourceSummaryDto
        {
            Code = entry.Code,
            DisplayName = entry.DisplayName,
            Provider = entry.Provider,
            Mode = def.Mode ?? string.Empty,
            Dataset = def.Dataset,
            Query = def.Query,
            Match = def.Match,
            ColumnCount = def.Columns?.Count ?? 0
        };
    }

    private static DocumentDataSourceDetailDto MapDataSourceDetail(DataSourceCatalogEntry entry)
    {
        var def = entry.Definition;
        var summary = MapDataSourceSummary(entry);
        return new DocumentDataSourceDetailDto
        {
            Code = summary.Code,
            DisplayName = summary.DisplayName,
            Provider = summary.Provider,
            Mode = summary.Mode,
            Dataset = summary.Dataset,
            Query = summary.Query,
            Match = summary.Match,
            ColumnCount = summary.ColumnCount,
            QueryName = def.QueryName,
            IdFrom = def.IdFrom,
            Parameters = def.Parameters,
            Columns = (def.Columns ?? new List<TemplateTableColumnModel>())
                .Select(c => new DocumentDataSourceColumnDto
                {
                    SourceField = c.SourceField,
                    Header = c.Header,
                    Format = c.Format
                })
                .ToList()
        };
    }

    public async Task<DocumentGenerationStatusDto> GetStatusAsync(
        string profileCode,
        string contextId,
        CancellationToken ct = default)
    {
        var profile = await ResolveProfileAsync(profileCode, ct);
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
        var profile = await ResolveProfileAsync(profileCode, ct);
        var templateCode = profile.TemplateCode;
        var (templateRow, model, resolved) = await BuildResolvedValuesAsync(
            profile,
            contextId.Trim(),
            templateCode,
            null,
            runtime: null,
            ct);
        var values = resolved.Scalars;

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

    public async Task<GenerateDocumentResultDto> RunGenerationAsync(
        DocumentGenerationRuntimeEnvelope envelope,
        CancellationToken ct = default)
    {
        var producerCode = envelope.ProducerCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(producerCode))
        {
            throw DocumentException.Validation(
                "PRODUCER_CODE_REQUIRED",
                "Producer code is required.",
                "Üretici kodu (producerCode) zorunludur.");
        }

        return await GenerateAsync(
            new GenerateDocumentRequest
            {
                ProfileCode = producerCode,
                TemplateCode = envelope.TemplateCode,
                Context = envelope.Context,
                Overrides = envelope.Overrides,
                Runtime = new DocumentGenerationRuntimeDto
                {
                    Scope = envelope.Scope,
                    Params = envelope.Params,
                    Trigger = envelope.Trigger
                }
            },
            ct);
    }

    public async Task<GenerateDocumentResultDto> GenerateAsync(
        GenerateDocumentRequest request,
        CancellationToken ct = default)
    {
        var profile = await ResolveProfileAsync(request.ProfileCode, ct);
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
        var (templateRow, model, resolved) = await BuildResolvedValuesAsync(
            profile,
            contextId,
            templateCode,
            request.Overrides,
            request.Runtime,
            ct);
        var values = resolved.Scalars;

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

        var outputFormat = NormalizeOutputFormat(profile.OutputFormat);
        byte[] merged;
        DocumentPlaceholderAnalysis.Result placeholderAnalysis;
        string mimeType;
        string extension;
        var coverResolve = new CoverPageResolveResult();

        if (IsXlsxFormat(outputFormat))
        {
            var rawTemplateBytes = await LoadTemplateBytesAsync(templateRow, ct);
            var xlsxBytes = XlsxTemplateBytesResolver.Resolve(rawTemplateBytes, templateRow);
            placeholderAnalysis = AnalyzeXlsxPlaceholders(xlsxBytes, model, values);
            merged = MergeScalarsAndSheetRows(
                xlsxBytes,
                model,
                resolved,
                placeholderAnalysis.PreservePlaceholderKeys);

            merged = XlsxImageParameterApplicator.Apply(merged, model, resolved);

            mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            extension = ".xlsx";
        }
        else if (IsPptxFormat(outputFormat))
        {
            var pptxBytes = await LoadTemplateBytesAsync(templateRow, ct);
            placeholderAnalysis = AnalyzePptxPlaceholders(pptxBytes, model, values);
            merged = PptxPlaceholderMerger.Merge(
                pptxBytes,
                resolved.Scalars,
                placeholderAnalysis.PreservePlaceholderKeys);

            merged = PptxImageParameterApplicator.Apply(merged, model, resolved);

            if (string.Equals(profile.Code, "odak.package.brief.fromPackage", StringComparison.OrdinalIgnoreCase))
                merged = PptxFulfillmentBarPatcher.Apply(merged, values);

            mimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            extension = ".pptx";
        }
        else
        {
            var docxBytes = await LoadTemplateDocxAsync(templateRow, ct);
            placeholderAnalysis = AnalyzePlaceholders(docxBytes, model, values);

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

                if (letterheadDesignDocx is { Length: > 0 }
                    && LetterheadDesignMerger.HasBrokenHeaderImages(branded))
                {
                    branded = LetterheadDesignMerger.EnsureHeaderWithMediaFromDesign(branded, letterheadDesignDocx);
                }
            }

            merged = MergeScalarsAndTables(
                branded,
                model,
                resolved,
                placeholderAnalysis.PreservePlaceholderKeys);

            coverResolve = await _coverPages.ResolveAsync(
                ShouldIncludeCoverPage(request.IncludeCoverPage, model.DefaultCoverPageId),
                request.CoverPageId,
                model.DefaultCoverPageId,
                ct);
            if (!string.IsNullOrWhiteSpace(coverResolve.CoverPageId))
            {
                merged = await ApplyCoverPageMergeAsync(
                    merged,
                    coverResolve,
                    values,
                    placeholderAnalysis.PreservePlaceholderKeys,
                    ct);
            }

            mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            extension = ".docx";
        }

        var remainingPlaceholders = outputFormat switch
        {
            "xlsx" => DocumentPlaceholderAnalysis.ScanRemainingXlsxPlaceholderKeys(merged),
            "pptx" => DocumentPlaceholderAnalysis.ScanRemainingPptxPlaceholderKeys(merged),
            _ => DocumentPlaceholderAnalysis.ScanRemainingPlaceholderKeys(merged)
        };

        var fileName = ApplyPattern(profile.FileNamePattern, values, contextId);
        if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            fileName += extension;

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
            MimeType = mimeType,
            Extension = extension,
            Size = merged.Length,
            Content = Convert.ToBase64String(merged),
            Origin = ResourceOrigin.Manual,
            TemplateId = templateRow.__dataId,
            TemplateCode = templateRow.code ?? templateCode,
            GenerationProfile = profile.Code,
            LetterheadId = letterheadResolve.LetterheadId,
            CoverPageId = coverResolve.CoverPageId,
            DocumentNo = businessDocNo
        }, ct);

        await WritebackAsync(profile, contextId, templateRow, templateCode, values, saved.Id, fileName, ct);

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
            CoverPageId = coverResolve.CoverPageId,
            CoverPageCode = coverResolve.CoverPageCode,
            CoverPageName = coverResolve.CoverPageName,
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

    public async Task<DocumentGenerationPreviewDto> PreviewFromTemplateAsync(
        string templateId,
        PreviewFromTemplateRequest? request,
        CancellationToken ct = default)
    {
        var (templateRow, model, resolved, placeholderAnalysis) = await BuildManualTemplateValuesAsync(
            templateId,
            request?.Overrides,
            allocateCounters: request?.AllocateCounters ?? false,
            documentName: null,
            ct);
        var values = resolved.Scalars;

        var missing = model.Parameters
            .Where(p => !values.TryGetValue(p.Key, out var v) || string.IsNullOrWhiteSpace(v))
            .Select(p => p.Key)
            .ToList();

        return new DocumentGenerationPreviewDto
        {
            ProfileCode = ManualProfileCode,
            ContextType = string.Empty,
            ContextId = string.Empty,
            Values = values,
            MissingKeys = missing,
            UndefinedParameterKeys = placeholderAnalysis.UndefinedParameterKeys,
            UnresolvedParameterKeys = placeholderAnalysis.UnresolvedParameterKeys
        };
    }

    public async Task<TemplateGenerationPreviewSessionDto> CreatePreviewSessionFromTemplateAsync(
        string templateId,
        PreviewFromTemplateRequest? request,
        CancellationToken ct = default)
    {
        var documentName = request?.DocumentName?.Trim();
        var (templateRow, model, resolved, placeholderAnalysis) = await BuildManualTemplateValuesAsync(
            templateId,
            request?.Overrides,
            allocateCounters: request?.AllocateCounters ?? false,
            documentName,
            ct);
        var values = resolved.Scalars;

        var docxBytes = await LoadTemplateDocxAsync(templateRow, ct);
        var mergedDocx = await MergeAndBrandAsync(templateRow, model, resolved, docxBytes, placeholderAnalysis, ct);
        var remainingPlaceholders = DocumentPlaceholderAnalysis.ScanRemainingPlaceholderKeys(mergedDocx);
        var fileName = ResolveManualFileName(documentName, values, templateRow);

        var session = await _templateEditor.CreateEphemeralPreviewSessionAsync(
            templateRow.__dataId ?? templateId,
            mergedDocx,
            fileName,
            ct);

        var missing = model.Parameters
            .Where(p => !values.TryGetValue(p.Key, out var v) || string.IsNullOrWhiteSpace(v))
            .Select(p => p.Key)
            .ToList();

        return new TemplateGenerationPreviewSessionDto
        {
            TemplateId = session.TemplateId,
            EditorUrl = session.EditorUrl,
            AccessToken = session.AccessToken,
            WopiSrc = session.WopiSrc,
            ReadOnly = session.ReadOnly,
            ProfileCode = ManualProfileCode,
            Values = values,
            MissingKeys = missing,
            UndefinedParameterKeys = placeholderAnalysis.UndefinedParameterKeys,
            UnresolvedParameterKeys = placeholderAnalysis.UnresolvedParameterKeys,
            RemainingPlaceholderKeys = remainingPlaceholders
        };
    }

    public async Task<GenerateDocumentResultDto> GenerateFromTemplateAsync(
        string templateId,
        GenerateFromTemplateRequest request,
        CancellationToken ct = default)
    {
        var parentFolderId = request.ParentFolderId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(parentFolderId))
        {
            throw DocumentException.Validation(
                "PARENT_FOLDER_REQUIRED",
                "Target folder id is required.",
                "Hedef klasör zorunludur.");
        }

        var documentName = request.DocumentName?.Trim();
        var (templateRow, model, resolved, placeholderAnalysis) = await BuildManualTemplateValuesAsync(
            templateId,
            request.Overrides,
            allocateCounters: true,
            documentName,
            ct);
        var values = resolved.Scalars;
        var outputFormat = await ResolveTemplateOutputFormatAsync(templateRow, model, ct);

        byte[] merged;
        string mimeType;
        string extension;
        var coverResolve = new CoverPageResolveResult();

        if (IsXlsxFormat(outputFormat))
        {
            var rawTemplateBytes = await LoadTemplateBytesAsync(templateRow, ct);
            var xlsxBytes = XlsxTemplateBytesResolver.Resolve(rawTemplateBytes, templateRow);
            merged = MergeScalarsAndSheetRows(
                xlsxBytes,
                model,
                resolved,
                placeholderAnalysis.PreservePlaceholderKeys);
            merged = XlsxImageParameterApplicator.Apply(merged, model, resolved);
            mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            extension = ".xlsx";
        }
        else if (IsPptxFormat(outputFormat))
        {
            var pptxBytes = await LoadTemplateBytesAsync(templateRow, ct);
            merged = PptxPlaceholderMerger.Merge(
                pptxBytes,
                resolved.Scalars,
                placeholderAnalysis.PreservePlaceholderKeys);
            merged = PptxImageParameterApplicator.Apply(merged, model, resolved);
            mimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            extension = ".pptx";
        }
        else
        {
            var docxBytes = await LoadTemplateDocxAsync(templateRow, ct);
            merged = await MergeAndBrandAsync(templateRow, model, resolved, docxBytes, placeholderAnalysis, ct);
            coverResolve = await _coverPages.ResolveAsync(
                ShouldIncludeCoverPage(request.IncludeCoverPage, model.DefaultCoverPageId),
                request.CoverPageId,
                model.DefaultCoverPageId,
                ct);
            if (!string.IsNullOrWhiteSpace(coverResolve.CoverPageId))
            {
                merged = await ApplyCoverPageMergeAsync(
                    merged,
                    coverResolve,
                    values,
                    placeholderAnalysis.PreservePlaceholderKeys,
                    ct);
            }

            mimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            extension = ".docx";
        }

        var remainingPlaceholders = outputFormat switch
        {
            "xlsx" => DocumentPlaceholderAnalysis.ScanRemainingXlsxPlaceholderKeys(merged),
            "pptx" => DocumentPlaceholderAnalysis.ScanRemainingPptxPlaceholderKeys(merged),
            _ => DocumentPlaceholderAnalysis.ScanRemainingPlaceholderKeys(merged)
        };

        var fileName = ResolveManualFileName(documentName, values, templateRow, extension);
        var displayName = ResolveManualDisplayName(documentName, fileName);
        var letterheadResolve = await _letterheads.ResolveAsync(
            model.DefaultLetterheadId,
            TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            ct);

        var saved = await _resources.CreateFileResourceAsync(new CreateFileResourceRequest
        {
            ParentId = parentFolderId,
            Name = displayName,
            OriginalFileName = fileName,
            MimeType = mimeType,
            Extension = extension,
            Size = merged.Length,
            Content = Convert.ToBase64String(merged),
            Origin = ResourceOrigin.Manual,
            TemplateId = templateRow.__dataId,
            TemplateCode = templateRow.code,
            GenerationProfile = ManualProfileCode,
            LetterheadId = letterheadResolve.LetterheadId,
            CoverPageId = coverResolve.CoverPageId,
            DocumentNo = ResolveBusinessDocNo(values)
        }, ct);

        var generatedAt = DateTime.UtcNow;

        return new GenerateDocumentResultDto
        {
            ProfileCode = ManualProfileCode,
            ContextType = string.Empty,
            ContextId = string.Empty,
            TemplateId = templateRow.__dataId ?? string.Empty,
            TemplateCode = templateRow.code ?? string.Empty,
            LetterheadId = letterheadResolve.LetterheadId,
            LetterheadCode = letterheadResolve.LetterheadCode,
            LetterheadName = letterheadResolve.LetterheadName,
            CoverPageId = coverResolve.CoverPageId,
            CoverPageCode = coverResolve.CoverPageCode,
            CoverPageName = coverResolve.CoverPageName,
            DocNo = ResolveBusinessDocNo(values),
            ResourceId = saved.Id,
            FileName = fileName,
            FolderPath = Array.Empty<string>(),
            GeneratedAt = generatedAt,
            ResolvedValues = values,
            UndefinedParameterKeys = placeholderAnalysis.UndefinedParameterKeys,
            UnresolvedParameterKeys = placeholderAnalysis.UnresolvedParameterKeys,
            RemainingPlaceholderKeys = remainingPlaceholders
        };
    }

    private async Task<(DmDocumentTemplate Template, TemplateModelDocument Model, ParameterResolutionResult Resolved, DocumentPlaceholderAnalysis.Result PlaceholderAnalysis)> BuildManualTemplateValuesAsync(
        string templateId,
        Dictionary<string, string>? overrides,
        bool allocateCounters,
        string? documentName,
        CancellationToken ct)
    {
        var templateRow = await LoadTemplateByIdAsync(templateId, ct);
        EnsureTemplatePublished(templateRow);

        var model = TemplateModelSerializer.Parse(templateRow.modelJson);
        var resolutionContext = CreateResolutionContext(
            contextType: string.Empty,
            contextId: string.Empty,
            contextTree: new JsonObject(),
            runtime: null);
        var resolved = await _parameterResolver.ResolveAsync(
            model,
            resolutionContext,
            profileDefaults: null,
            overrides,
            Token,
            ct);
        var values = resolved.Scalars;

        if (!string.IsNullOrWhiteSpace(documentName))
            values[LetterheadConstants.DocumentNameKey] = documentName.Trim();

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
            allocateCounters,
            Token,
            ct);

        var outputFormat = await ResolveTemplateOutputFormatAsync(templateRow, model, ct);
        DocumentPlaceholderAnalysis.Result placeholderAnalysis;
        if (IsXlsxFormat(outputFormat))
        {
            var rawTemplateBytes = await LoadTemplateBytesAsync(templateRow, ct);
            var xlsxBytes = XlsxTemplateBytesResolver.Resolve(rawTemplateBytes, templateRow);
            placeholderAnalysis = AnalyzeXlsxPlaceholders(xlsxBytes, model, values);
        }
        else if (IsPptxFormat(outputFormat))
        {
            var pptxBytes = await LoadTemplateBytesAsync(templateRow, ct);
            placeholderAnalysis = AnalyzePptxPlaceholders(pptxBytes, model, values);
        }
        else
        {
            var docxBytes = await LoadTemplateDocxAsync(templateRow, ct);
            placeholderAnalysis = AnalyzePlaceholders(docxBytes, model, values);
        }

        return (templateRow, model, resolved, placeholderAnalysis);
    }

    private async Task<byte[]> MergeAndBrandAsync(
        DmDocumentTemplate templateRow,
        TemplateModelDocument model,
        ParameterResolutionResult resolved,
        byte[] docxBytes,
        DocumentPlaceholderAnalysis.Result placeholderAnalysis,
        CancellationToken ct)
    {
        var values = resolved.Scalars;
        var letterheadResolve = await _letterheads.ResolveAsync(
            model.DefaultLetterheadId,
            TemplateModelSerializer.ToLetterheadDto(model.Letterhead),
            ct);

        var letterheadModel = letterheadResolve.Letterhead is { Enabled: true }
            ? TemplateModelSerializer.ToLetterheadModel(letterheadResolve.Letterhead)
            : null;
        var (footerModel, pageLayout) = LetterheadBrandingResolver.Resolve(letterheadResolve, model);

        var branded = docxBytes;
        if (letterheadModel is not null || footerModel is not null || !string.IsNullOrWhiteSpace(letterheadResolve.LetterheadId))
        {
            var letterheadEntry = await TryLoadLetterheadEntryAsync(letterheadResolve, ct);
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

            if (letterheadDesignDocx is { Length: > 0 }
                && LetterheadDesignMerger.HasBrokenHeaderImages(branded))
            {
                branded = LetterheadDesignMerger.EnsureHeaderWithMediaFromDesign(branded, letterheadDesignDocx);
            }
        }

        return MergeScalarsAndTables(
            branded,
            model,
            resolved,
            placeholderAnalysis.PreservePlaceholderKeys);
    }

    private static bool ShouldIncludeCoverPage(bool? requestInclude, string? templateDefaultCoverPageId)
    {
        if (requestInclude == false)
            return false;

        if (requestInclude == true)
            return true;

        return !string.IsNullOrWhiteSpace(templateDefaultCoverPageId);
    }

    private async Task<byte[]> ApplyCoverPageMergeAsync(
        byte[] mergedDocx,
        CoverPageResolveResult coverResolve,
        IReadOnlyDictionary<string, string> values,
        IReadOnlySet<string>? preservePlaceholderKeys,
        CancellationToken ct)
    {
        var coverDesignBytes = await TryLoadCoverDesignDocxAsync(coverResolve.CoverPage, ct);
        if (coverDesignBytes is not { Length: > 0 })
            return mergedDocx;

        var filledCover = DocxPlaceholderMerger.Merge(
            coverDesignBytes,
            values,
            preservePlaceholderKeys);
        return CoverPageMerger.Prepend(mergedDocx, filledCover);
    }

    private static byte[] MergeScalarsAndTables(
        byte[] docxBytes,
        TemplateModelDocument model,
        ParameterResolutionResult resolved,
        IReadOnlySet<string>? preservePlaceholderKeys) =>
        DocxTableExpander.Expand(
            DocxPlaceholderMerger.Merge(docxBytes, resolved.Scalars, preservePlaceholderKeys),
            model,
            resolved.Tables);

    private static byte[] MergeScalarsAndSheetRows(
        byte[] xlsxBytes,
        TemplateModelDocument model,
        ParameterResolutionResult resolved,
        IReadOnlySet<string>? preservePlaceholderKeys) =>
        XlsxTableExpander.Expand(
            XlsxPlaceholderMerger.Merge(xlsxBytes, resolved.Scalars, preservePlaceholderKeys),
            model,
            resolved.Tables);

    private static bool IsXlsxFormat(string outputFormat) =>
        string.Equals(outputFormat, "xlsx", StringComparison.OrdinalIgnoreCase);

    private static bool IsPptxFormat(string outputFormat) =>
        string.Equals(outputFormat, "pptx", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOutputFormat(string? format)
    {
        var normalized = format?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "xlsx" => "xlsx",
            "pptx" => "pptx",
            _ => "docx"
        };
    }

    private static DocumentPlaceholderAnalysis.Result AnalyzePptxPlaceholders(
        byte[] pptxBytes,
        TemplateModelDocument model,
        IReadOnlyDictionary<string, string> values)
    {
        using var stream = new MemoryStream(pptxBytes, writable: false);
        var scan = PptxPlaceholderScanner.Scan(stream);
        return DocumentPlaceholderAnalysis.AnalyzePptx(scan, model, values);
    }

    private static DocumentPlaceholderAnalysis.Result AnalyzeXlsxPlaceholders(
        byte[] xlsxBytes,
        TemplateModelDocument model,
        IReadOnlyDictionary<string, string> values)
    {
        using var stream = new MemoryStream(xlsxBytes, writable: false);
        var scan = XlsxPlaceholderScanner.Scan(stream);
        return DocumentPlaceholderAnalysis.AnalyzeXlsx(scan, model, values);
    }

    private static void EnsureTemplatePublished(DmDocumentTemplate templateRow)
    {
        var status = templateRow.status?.Trim() ?? string.Empty;
        if (!string.Equals(status, TemplateStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "TEMPLATE_NOT_PUBLISHED",
                "Template must be published before document generation.",
                "Belge üretimi için şablon yayımlanmış olmalıdır.");
        }
    }

    private static string ResolveManualFileName(
        string? documentName,
        IReadOnlyDictionary<string, string> values,
        DmDocumentTemplate template,
        string extension = ".docx")
    {
        var normalizedExt = extension.StartsWith('.') ? extension : "." + extension;
        if (!string.IsNullOrWhiteSpace(documentName))
        {
            var baseName = SanitizeFileStem(documentName.Trim());
            return baseName.EndsWith(normalizedExt, StringComparison.OrdinalIgnoreCase)
                ? baseName
                : baseName + normalizedExt;
        }

        if (values.TryGetValue(LetterheadConstants.DocNoKey, out var docNo) && !string.IsNullOrWhiteSpace(docNo))
            return $"{SanitizeFileStem(docNo)}{normalizedExt}";

        var code = template.code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            code = template.name?.Trim() ?? "document";

        return $"{SanitizeFileStem(code)}-{DateTime.UtcNow:yyyyMMdd-HHmmss}{normalizedExt}";
    }

    private async Task<string> ResolveTemplateOutputFormatAsync(
        DmDocumentTemplate template,
        TemplateModelDocument model,
        CancellationToken ct)
    {
        var fromFile = InferOutputFormatFromFileName(
            template.sourceFileName,
            template.sourceStoragePath);
        if (!string.Equals(fromFile, "docx", StringComparison.OrdinalIgnoreCase))
            return fromFile;

        if (!string.IsNullOrWhiteSpace(model.GenerationProfile))
        {
            var profile = await _producerCatalog.TryGetAsync(model.GenerationProfile.Trim(), ct);
            if (profile is not null)
                return NormalizeOutputFormat(profile.OutputFormat);
        }

        return fromFile;
    }

    private static string InferOutputFormatFromFileName(string? sourceFileName, string? sourceStoragePath)
    {
        var pathOrName = sourceFileName?.Trim();
        if (string.IsNullOrWhiteSpace(pathOrName))
            pathOrName = sourceStoragePath?.Trim();
        if (string.IsNullOrWhiteSpace(pathOrName))
            return "docx";

        var ext = Path.GetExtension(pathOrName).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "xlsx" or "xlsm" => "xlsx",
            "pptx" => "pptx",
            _ => "docx"
        };
    }

    private static string ResolveManualDisplayName(string? documentName, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(documentName))
            return documentName.Trim();

        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string SanitizeFileStem(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var stem = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(stem) ? "document" : stem;
    }

    private async Task<DmDocumentTemplate> LoadTemplateByIdAsync(string templateId, CancellationToken ct)
    {
        var id = templateId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw DocumentException.Validation(
                "TEMPLATE_ID_REQUIRED",
                "Template id is required.",
                "Şablon kimliği zorunludur.");
        }

        var row = await _dg.GetByIdAsync<DmDocumentTemplate>(DmDatasets.DocumentTemplates, id, Token, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
        {
            throw DocumentException.Validation(
                "TEMPLATE_NOT_FOUND",
                $"Template not found: {id}",
                $"Şablon bulunamadı.");
        }

        return row;
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

    private async Task<(DmDocumentTemplate Template, TemplateModelDocument Model, ParameterResolutionResult Resolved)> BuildResolvedValuesAsync(
        DocumentGenerationProfileSettings profile,
        string contextId,
        string templateCode,
        Dictionary<string, string>? overrides,
        DocumentGenerationRuntimeDto? runtime,
        CancellationToken ct)
    {
        var contextDef = await _contextCatalog.GetRequiredAsync(profile.ContextType, ct);
        var contextTree = await _contextLoader.LoadAsync(contextDef, contextId, Token, ct);
        var templateRow = await LoadTemplateByCodeAsync(templateCode, ct);
        var model = TemplateModelSerializer.Parse(templateRow.modelJson);
        var resolutionContext = CreateResolutionContext(
            profile.ContextType,
            contextId,
            contextTree,
            runtime);
        var resolved = await _parameterResolver.ResolveAsync(
            model,
            resolutionContext,
            profile.Defaults,
            overrides,
            Token,
            ct);

        EnrichPatternTokens(resolved.Scalars, contextTree);

        if (_packageDashboardEnricher.AppliesTo(profile.Code))
        {
            await _packageDashboardEnricher.EnrichAsync(
                resolved,
                contextId.Trim(),
                contextTree,
                Token,
                ct);
        }

        return (templateRow, model, resolved);
    }

    private ParameterResolutionContext CreateResolutionContext(
        string contextType,
        string contextId,
        JsonObject contextTree,
        DocumentGenerationRuntimeDto? runtime) =>
        new()
        {
            ContextType = contextType,
            ContextId = contextId,
            ContextTree = contextTree,
            WorkspaceId = runtime?.Scope?.WorkspaceId,
            DomainId = runtime?.Scope?.DomainId ?? _ctx.DomainId,
            UserId = _ctx.UserId,
            Params = runtime?.Params is { Count: > 0 }
                ? new Dictionary<string, string>(runtime.Params, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

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

    private async Task<byte[]?> TryLoadCoverDesignDocxAsync(CoverPageDto? coverPage, CancellationToken ct)
    {
        if (coverPage is null || string.IsNullOrWhiteSpace(coverPage.Id))
            return null;

        var row = await _dg.GetByIdAsync<DmCoverPage>(DmDatasets.CoverPages, coverPage.Id, Token, ct);
        if (row is null)
            return null;

        var bytes = await CoverPageDesignFileLoader.DownloadDesignAsync(_dg, row, Token, ct);
        if (bytes is not { Length: > 0 })
            return null;

        bytes = DocxZipHelper.DeduplicateParts(bytes);
        return bytes;
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
        TryAdd("packageNo", "packageNo");
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

    private async Task<byte[]> LoadTemplateBytesAsync(DmDocumentTemplate template, CancellationToken ct)
    {
        var path = template.sourceStoragePath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            var (resolvedPath, _) = await ResolveStoragePathFallbackAsync(template, ct);
            path = resolvedPath;
        }

        return await _dg.DownloadFileAsync(path!, Token, ct);
    }

    private async Task<byte[]> LoadTemplateDocxAsync(DmDocumentTemplate template, CancellationToken ct)
    {
        var bytes = await LoadTemplateBytesAsync(template, ct);
        return await EnsureTemplateHeaderMediaAsync(template, bytes, ct);
    }

    private async Task<byte[]> EnsureTemplateHeaderMediaAsync(
        DmDocumentTemplate template,
        byte[] docxBytes,
        CancellationToken ct)
    {
        if (!LetterheadDesignMerger.HasBrokenHeaderImages(docxBytes))
            return docxBytes;

        var model = TemplateModelSerializer.Parse(template.modelJson);
        if (string.IsNullOrWhiteSpace(model.DefaultLetterheadId))
            return docxBytes;

        var letterheadRow = await _dg.GetByIdAsync<DmLetterhead>(
            DmDatasets.Letterheads,
            model.DefaultLetterheadId,
            Token,
            ct);
        if (letterheadRow is null || string.IsNullOrWhiteSpace(letterheadRow.designStoragePath))
            return docxBytes;

        var designBytes = await _dg.DownloadFileAsync(letterheadRow.designStoragePath, Token, ct);
        return LetterheadDesignMerger.EnsureHeaderWithMediaFromDesign(docxBytes, designBytes);
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
            var children = await _resources.GetChildrenAsync(parentId, limit: null, ct: ct);
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
        string fileName,
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
                case "shipmentlistdiresourceid":
                    payload["shipmentListDiResourceId"] = resourceId;
                    break;
                case "shipmentlistfilename":
                    payload["shipmentListFileName"] = fileName;
                    break;
                case "shipmentlistgeneratedat":
                    payload["shipmentListGeneratedAt"] = DateTime.UtcNow;
                    break;
                case "shipmentlisttemplatecode":
                    payload["shipmentListTemplateCode"] = templateRow.code ?? templateCode;
                    break;
                case "shipmentlisttemplatename":
                    payload["shipmentListTemplateName"] = templateRow.name ?? string.Empty;
                    break;
                case "packagedashboarddiresourceid":
                    payload["packageDashboardDiResourceId"] = resourceId;
                    break;
                case "packagedashboardfilename":
                    payload["packageDashboardFileName"] = fileName;
                    break;
                case "packagedashboardgeneratedat":
                    payload["packageDashboardGeneratedAt"] = DateTime.UtcNow;
                    break;
                case "packagedashboardtemplatecode":
                    payload["packageDashboardTemplateCode"] = templateRow.code ?? templateCode;
                    break;
                case "packagedashboardtemplatename":
                    payload["packageDashboardTemplateName"] = templateRow.name ?? string.Empty;
                    break;
                case "packagebriefdiresourceid":
                    payload["packageBriefDiResourceId"] = resourceId;
                    break;
                case "packagebrieffilename":
                    payload["packageBriefFileName"] = fileName;
                    break;
                case "packagebriefgeneratedat":
                    payload["packageBriefGeneratedAt"] = DateTime.UtcNow;
                    break;
                case "packagebrieftemplatecode":
                    payload["packageBriefTemplateCode"] = templateRow.code ?? templateCode;
                    break;
                case "packagebrieftemplatename":
                    payload["packageBriefTemplateName"] = templateRow.name ?? string.Empty;
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

    private async Task<DocumentGenerationProfileSettings> ResolveProfileAsync(
        string? profileCode,
        CancellationToken ct)
    {
        var code = profileCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "PROFILE_CODE_REQUIRED",
                "Profile code is required.",
                "Üretim profili kodu zorunludur.");
        }

        var profile = await _producerCatalog.TryGetAsync(code, ct);

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
