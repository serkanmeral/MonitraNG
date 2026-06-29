using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;

namespace MngDocument.Infrastructure.Services;

public sealed class TemplateEditorService : ITemplateEditorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private const string SchemaVersion = "1.0";
    private const string DefaultFileName = "document.docx";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IWopiSessionStore _sessions;
    private readonly IDocumentTemplateService _templates;
    private readonly ITemplateBrandingApplier _brandingApplier;
    private readonly MngDocumentSettings _settings;

    public TemplateEditorService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IWopiSessionStore sessions,
        IDocumentTemplateService templates,
        ITemplateBrandingApplier brandingApplier,
        IOptions<MngDocumentSettings> settings)
    {
        _dg = dg;
        _ctx = ctx;
        _sessions = sessions;
        _templates = templates;
        _brandingApplier = brandingApplier;
        _settings = settings.Value;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<TemplateDetailDto> CreateBlankAsync(CreateBlankTemplateRequest request, CancellationToken ct = default)
    {
        EnsureCollaboraEnabled();

        var categoryId = request.CategoryId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            throw DocumentException.Validation(
                "CATEGORY_REQUIRED",
                "categoryId is required.",
                "Kategori seçimi zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        await EnsureCategoryExistsAsync(categoryId, ct);

        var code = request.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            throw DocumentException.Validation(
                "CODE_REQUIRED",
                "Template code is required.",
                "Şablon kodu zorunludur.");
        }

        await EnsureCodeUniqueInCategoryAsync(categoryId, code, excludeTemplateId: null, ct);

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = code;

        var displayFileName = TemplateFileNameHelper.ResolveDisplayFileName(name, code, sourceFileName: null);

        var letterheadModel = TemplateModelSerializer.ToLetterheadModel(request.Letterhead);
        var footerModel = TemplateModelSerializer.ToFooterModel(request.Footer);
        var model = TemplateModelSerializer.BuildWithBranding(letterheadModel, footerModel);
        var modelJson = TemplateModelSerializer.Serialize(model);

        var docxBytes = MinimalDocxFactory.CreateBlank();
        docxBytes = await _brandingApplier.ApplyAsync(
            docxBytes,
            name,
            letterheadModel is { Enabled: true } ? letterheadModel : null,
            footerModel is { Enabled: true } ? footerModel : null,
            model.PageLayout,
            Token,
            ct);

        var referenceFile = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(docxBytes),
            ["originalFileName"] = displayFileName
        };

        var now = DateTime.UtcNow;
        var payload = BuildCreatePayload(
            name,
            code,
            description: null,
            categoryId,
            creationMode: TemplateCreationMode.Blank,
            modelJson,
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
                ["sourceFileName"] = storedName ?? TemplateFileNameHelper.ResolveDisplayFileName(name, code, DefaultFileName),
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

        return await _templates.GetByIdAsync(created.__dataId!, ct);
    }

    public async Task<TemplateEditorSessionDto> CreateEditorSessionAsync(string templateId, CancellationToken ct = default)
    {
        EnsureCollaboraEnabled();

        var id = templateId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        var detail = await _templates.GetByIdAsync(id, ct);
        var readOnly = string.Equals(detail.Status, TemplateStatus.Published, StringComparison.OrdinalIgnoreCase);

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        var userName = _ctx.Username ?? userId;

        var session = new WopiSession
        {
            TemplateId = id,
            UserId = userId,
            UserName = userName,
            DataGatewayToken = Token,
            Version = "1",
            ReadOnly = readOnly
        };

        var ttl = TimeSpan.FromMinutes(Math.Clamp(_settings.Wopi.SessionMinutes, 15, 1440));
        var accessToken = _sessions.CreateSession(session, ttl);

        var wopiHost = _settings.Wopi.HostBaseUrl.TrimEnd('/');
        var wopiSrc = $"{wopiHost}/wopi/files/{Uri.EscapeDataString(id)}";

        var collaboraBase = _settings.Collabora.PublicBaseUrl.TrimEnd('/');
        var editorPath = _settings.Collabora.EditorPath.StartsWith('/')
            ? _settings.Collabora.EditorPath
            : $"/{_settings.Collabora.EditorPath}";

        var editorUrl = new StringBuilder()
            .Append(collaboraBase)
            .Append(editorPath)
            .Append("?WOPISrc=")
            .Append(WebUtility.UrlEncode(wopiSrc))
            .Append("&access_token=")
            .Append(WebUtility.UrlEncode(accessToken))
            .Append(readOnly ? "&permission=readonly" : "&permission=edit")
            .Append("&lang=tr")
            .Append("&ui_defaults=")
            .Append(WebUtility.UrlEncode("UIMode=compact;TextSidebar=true;TextStatusbar=false"))
            .ToString();

        return new TemplateEditorSessionDto
        {
            TemplateId = id,
            EditorUrl = editorUrl,
            AccessToken = accessToken,
            WopiSrc = wopiSrc,
            ReadOnly = readOnly
        };
    }

    public async Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string templateId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureTemplateSession(templateId, session);
        var template = await LoadTemplateAsync(templateId, session.DataGatewayToken, ct);
        var readOnly = string.Equals(template.status, TemplateStatus.Published, StringComparison.OrdinalIgnoreCase);
        var fileName = TemplateFileNameHelper.ResolveDisplayFileName(
            template.name,
            template.code,
            template.sourceFileName);
        var bytes = await GetDocxBytesAsync(template, session.DataGatewayToken, ct);

        return new WopiCheckFileInfoDto
        {
            BaseFileName = fileName,
            Size = bytes.LongLength,
            OwnerId = session.UserId,
            UserId = session.UserId,
            UserFriendlyName = session.UserName,
            Version = session.Version,
            SupportsUpdate = !readOnly,
            UserCanWrite = !readOnly,
            UserCanNotWriteRelative = false,
            SupportsLocks = false,
            SupportsRename = false,
            UserCanRename = false
        };
    }

    public async Task<byte[]> GetFileContentsAsync(
        string templateId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureTemplateSession(templateId, session);
        var template = await LoadTemplateAsync(templateId, session.DataGatewayToken, ct);
        return await GetDocxBytesAsync(template, session.DataGatewayToken, ct);
    }

    public async Task SaveFileContentsAsync(
        string templateId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default)
    {
        EnsureTemplateSession(templateId, session);
        var template = await LoadTemplateAsync(templateId, session.DataGatewayToken, ct);
        TemplateDraftGuard.EnsureDraft(template);
        var fileName = TemplateFileNameHelper.ResolveDisplayFileName(
            template.name,
            template.code,
            template.sourceFileName);

        var referenceFile = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(content),
            ["originalFileName"] = fileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["categoryId"] = template.categoryId,
            ["name"] = template.name,
            ["code"] = template.code,
            ["description"] = template.description,
            ["sourceResourceId"] = template.sourceResourceId,
            ["sourceFileName"] = fileName,
            ["creationMode"] = template.creationMode ?? TemplateCreationMode.Blank,
            ["status"] = template.status ?? "draft",
            ["modelJson"] = template.modelJson,
            ["referenceFile"] = referenceFile,
            ["updatedBy"] = session.UserName,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmDocumentTemplate>(
            DmDatasets.DocumentTemplates,
            templateId,
            payload,
            session.DataGatewayToken,
            ct);

        var (path, storedName) = DgFileFieldReader.Read(updated);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var patch = new Dictionary<string, object?>
            {
                ["sourceStoragePath"] = path,
                ["sourceFileName"] = storedName ?? fileName,
                ["updatedBy"] = session.UserName,
                ["updatedAt"] = DateTime.UtcNow
            };
            updated = await _dg.UpdateAsync<DmDocumentTemplate>(
                DmDatasets.DocumentTemplates,
                templateId,
                patch,
                session.DataGatewayToken,
                ct);
        }

        var newVersion = (long.TryParse(session.Version, out var current) ? current + 1 : 2).ToString();
        if (!string.IsNullOrWhiteSpace(accessToken))
            _sessions.BumpVersion(accessToken, newVersion);
        else
            session.Version = newVersion;
    }

    private void EnsureCollaboraEnabled()
    {
        if (!_settings.Collabora.Enabled)
        {
            throw DocumentException.Validation(
                "COLLABORA_DISABLED",
                "Collabora editor is not enabled.",
                "Belge editörü etkin değil.");
        }
    }

    private static void EnsureTemplateSession(string templateId, WopiSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ResourceId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.Equals(templateId, session.TemplateId, StringComparison.Ordinal))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");
    }

    private async Task EnsureCategoryExistsAsync(string categoryId, CancellationToken ct)
    {
        var cat = await _dg.GetByIdAsync<DmTemplateCategory>(DmDatasets.TemplateCategories, categoryId, Token, ct);
        if (cat is null || cat.__dataId is null)
            throw DocumentException.NotFound("Kategori bulunamadı.");
    }

    private async Task<DmDocumentTemplate> LoadTemplateAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<DmDocumentTemplate>(DmDatasets.DocumentTemplates, id, token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Şablon bulunamadı.");
        return row;
    }

    private async Task<byte[]> GetDocxBytesAsync(DmDocumentTemplate template, string token, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(template.sourceStoragePath))
            return await _dg.DownloadFileAsync(template.sourceStoragePath, token, ct);

        var (path, _) = DgFileFieldReader.Read(template);
        if (!string.IsNullOrWhiteSpace(path))
            return await _dg.DownloadFileAsync(path!, token, ct);

        throw DocumentException.Validation(
            "SOURCE_FILE_MISSING",
            "Template source file is missing.",
            "Şablon dosyası bulunamadı.");
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
                var id = idObj.ToString();
                if (!string.IsNullOrWhiteSpace(excludeTemplateId)
                    && string.Equals(id, excludeTemplateId, StringComparison.Ordinal))
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
            ["code"] = code,
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
}
