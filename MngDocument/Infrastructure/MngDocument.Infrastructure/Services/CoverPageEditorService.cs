using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.CoverPages;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;

namespace MngDocument.Infrastructure.Services;

public sealed class CoverPageEditorService : ICoverPageEditorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IWopiSessionStore _sessions;
    private readonly IEditorSessionService _editorSessions;
    private readonly ICoverPageService _coverPages;
    private readonly IDomainLogoProvider _logoProvider;
    private readonly MngDocumentSettings _settings;

    public CoverPageEditorService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IWopiSessionStore sessions,
        IEditorSessionService editorSessions,
        ICoverPageService coverPages,
        IDomainLogoProvider logoProvider,
        IOptions<MngDocumentSettings> settings)
    {
        _dg = dg;
        _ctx = ctx;
        _sessions = sessions;
        _editorSessions = editorSessions;
        _coverPages = coverPages;
        _logoProvider = logoProvider;
        _settings = settings.Value;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<CoverPageDesignSessionDto> CreateDesignSessionAsync(string coverPageId, CancellationToken ct = default)
    {
        EnsureCollaboraEnabled();

        var id = coverPageId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        _ = await _coverPages.GetByIdAsync(id, ct);
        await EnsureDesignFileAsync(id, ct);

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        var userName = _ctx.Username ?? userId;

        var session = new WopiSession
        {
            TemplateId = string.Empty,
            CoverPageId = id,
            UserId = userId,
            UserName = userName,
            DataGatewayToken = Token,
            Version = "1",
            ReadOnly = false
        };

        var ttl = TimeSpan.FromMinutes(Math.Clamp(_settings.Wopi.SessionMinutes, 15, 1440));
        var accessToken = _editorSessions.BeginSession(session, ttl);

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
            .Append("&permission=edit")
            .Append("&lang=tr")
            .Append("&ui_defaults=")
            .Append(WebUtility.UrlEncode("UIMode=compact;TextSidebar=true;TextStatusbar=false"))
            .ToString();

        return new CoverPageDesignSessionDto
        {
            CoverPageId = id,
            EditorUrl = editorUrl,
            AccessToken = accessToken,
            WopiSrc = wopiSrc,
            ReadOnly = false
        };
    }

    public async Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string coverPageId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureCoverPageSession(coverPageId, session);
        var row = await LoadCoverPageAsync(coverPageId, session.DataGatewayToken, ct);
        var fileName = ResolveDesignFileName(row);
        var raw = await GetDesignDocxBytesAsync(row, session.DataGatewayToken, ct);
        var bytes = await PrepareDesignDocxForEditorAsync(row, raw, session.DataGatewayToken, ct);
        var version = ResolveDesignVersion(row, session);

        return new WopiCheckFileInfoDto
        {
            BaseFileName = fileName,
            Size = bytes.LongLength,
            OwnerId = session.UserId,
            UserId = session.UserId,
            UserFriendlyName = session.UserName,
            Version = version,
            SupportsUpdate = true,
            UserCanWrite = true,
            UserCanNotWriteRelative = false,
            SupportsLocks = false,
            SupportsRename = false,
            UserCanRename = false,
            PostMessageOrigin = WopiCollaboraHelper.ResolvePostMessageOrigin(session, _settings.Collabora)
        };
    }

    public async Task<byte[]> GetFileContentsAsync(
        string coverPageId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureCoverPageSession(coverPageId, session);
        var row = await LoadCoverPageAsync(coverPageId, session.DataGatewayToken, ct);
        var raw = await GetDesignDocxBytesAsync(row, session.DataGatewayToken, ct);
        return await PrepareDesignDocxForEditorAsync(row, raw, session.DataGatewayToken, ct);
    }

    public async Task SaveFileContentsAsync(
        string coverPageId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default)
    {
        EnsureCoverPageSession(coverPageId, session);
        var row = await LoadCoverPageAsync(coverPageId, session.DataGatewayToken, ct);
        var fileName = ResolveDesignFileName(row);
        content = DocxZipHelper.DeduplicateParts(content);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = row.name,
            ["code"] = row.code,
            ["description"] = row.description,
            ["isDefault"] = row.isDefault,
            ["isActive"] = row.isActive,
            ["coverPageJson"] = row.coverPageJson,
            ["settingsJson"] = row.settingsJson,
            ["designFileName"] = fileName,
            ["designFile"] = new Dictionary<string, object?>
            {
                ["content"] = Convert.ToBase64String(content),
                ["originalFileName"] = fileName
            },
            ["updatedBy"] = session.UserName,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmCoverPage>(
            DmDatasets.CoverPages,
            coverPageId,
            payload,
            session.DataGatewayToken,
            ct);

        var (path, storedName) = DgFileFieldReader.Read(updated);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _dg.UpdateAsync<DmCoverPage>(
                DmDatasets.CoverPages,
                coverPageId,
                new Dictionary<string, object?>
                {
                    ["designStoragePath"] = path,
                    ["designFileName"] = storedName ?? fileName,
                    ["updatedBy"] = session.UserName,
                    ["updatedAt"] = DateTime.UtcNow
                },
                session.DataGatewayToken,
                ct);
        }

        var savedRow = await LoadCoverPageAsync(coverPageId, session.DataGatewayToken, ct);
        var newVersion = ResolveDesignVersion(savedRow, session);
        if (!string.IsNullOrWhiteSpace(accessToken))
            _sessions.BumpVersion(accessToken, newVersion);
        else
            session.Version = newVersion;
    }

    private async Task EnsureDesignFileAsync(string coverPageId, CancellationToken ct)
    {
        var row = await LoadCoverPageAsync(coverPageId, Token!, ct);
        if (!string.IsNullOrWhiteSpace(row.designStoragePath))
            return;

        var (pathFromField, _) = DgFileFieldReader.Read(row);
        if (!string.IsNullOrWhiteSpace(pathFromField))
            return;

        var dto = await _coverPages.GetByIdAsync(coverPageId, ct);
        var skeleton = await BuildSkeletonDocxAsync(dto, ct);
        var fileName = ResolveDesignFileName(row);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = row.name,
            ["code"] = row.code,
            ["description"] = row.description,
            ["isDefault"] = row.isDefault,
            ["isActive"] = row.isActive,
            ["coverPageJson"] = row.coverPageJson,
            ["settingsJson"] = row.settingsJson,
            ["designFileName"] = fileName,
            ["designFile"] = new Dictionary<string, object?>
            {
                ["content"] = Convert.ToBase64String(skeleton),
                ["originalFileName"] = fileName
            },
            ["updatedBy"] = _ctx.Username,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmCoverPage>(
            DmDatasets.CoverPages,
            coverPageId,
            payload,
            Token,
            ct);

        var (path, storedName) = DgFileFieldReader.Read(updated);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw DocumentException.Validation(
                "COVER_PAGE_DESIGN_INIT_FAILED",
                "Cover page design file could not be initialized.",
                "Kapak sayfası tasarım dosyası oluşturulamadı.");
        }

        await _dg.UpdateAsync<DmCoverPage>(
            DmDatasets.CoverPages,
            coverPageId,
            new Dictionary<string, object?>
            {
                ["designStoragePath"] = path,
                ["designFileName"] = storedName ?? fileName,
                ["updatedBy"] = _ctx.Username,
                ["updatedAt"] = DateTime.UtcNow
            },
            Token,
            ct);
    }

    private async Task<byte[]> BuildSkeletonDocxAsync(CoverPageDto dto, CancellationToken ct)
    {
        DomainLogoResult? logo = null;
        if (dto.Definition.ShowLogo)
            logo = await _logoProvider.GetCurrentDomainLogoAsync(Token, ct);

        return CoverPageDesignSkeletonBuilder.Build(
            dto.Definition,
            dto.Settings,
            dto.Name,
            logo?.Bytes,
            logo?.Extension ?? ".png");
    }

    private async Task<byte[]> PrepareDesignDocxForEditorAsync(
        DmCoverPage row,
        byte[] rawDocxBytes,
        string token,
        CancellationToken ct)
    {
        var dto = MapRowToDto(row);

        // Stored designs must round-trip unchanged through Collabora WOPI (no logo/layout rewrite on read).
        if (dto.HasDesign)
            return rawDocxBytes;

        if (!dto.Definition.ShowLogo)
            return rawDocxBytes;

        var logo = await _logoProvider.GetCurrentDomainLogoAsync(token, ct);
        if (logo is not { Bytes.Length: > 0 })
            return rawDocxBytes;

        return CoverPageLogoInjector.EnsureLogoForUse(
            rawDocxBytes,
            logo.Bytes,
            logo.Extension,
            bootstrapIfMissing: true);
    }

    private static CoverPageDto MapRowToDto(DmCoverPage row)
    {
        var (_, designNameFromField) = DgFileFieldReader.Read(row);
        var designStoragePath = CoverPageDesignFileLoader.ResolveDesignPath(row);

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
            DesignFileName = row.designFileName ?? designNameFromField,
            HasDesign = !string.IsNullOrWhiteSpace(designStoragePath),
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };
    }

    private static void EnsureCoverPageSession(string coverPageId, WopiSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CoverPageId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.IsNullOrWhiteSpace(session.ResourceId)
            || !string.IsNullOrWhiteSpace(session.TemplateId)
            || !string.IsNullOrWhiteSpace(session.LetterheadId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.Equals(coverPageId, session.CoverPageId, StringComparison.Ordinal))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");
    }

    private async Task<DmCoverPage> LoadCoverPageAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<DmCoverPage>(DmDatasets.CoverPages, id, token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Kapak sayfası bulunamadı.");
        return row;
    }

    private async Task<byte[]> GetDesignDocxBytesAsync(DmCoverPage row, string token, CancellationToken ct)
    {
        var bytes = await CoverPageDesignFileLoader.DownloadDesignAsync(_dg, row, token, ct);
        if (bytes is { Length: > 0 })
            return DocxZipHelper.DeduplicateParts(bytes);

        throw DocumentException.Validation(
            "COVER_PAGE_DESIGN_MISSING",
            "Cover page design file is missing.",
            "Kapak sayfası tasarım dosyası bulunamadı.");
    }

    private static string ResolveDesignFileName(DmCoverPage row)
    {
        if (!string.IsNullOrWhiteSpace(row.designFileName))
            return row.designFileName!;

        var code = row.code?.Trim();
        return string.IsNullOrWhiteSpace(code) ? "cover-page-design.docx" : $"{code}-cover.docx";
    }

    private static string ResolveDesignVersion(DmCoverPage row, WopiSession session)
    {
        var (path, _) = DgFileFieldReader.Read(row);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var stamp = row.updatedAt?.ToUniversalTime().ToString("yyyyMMddHHmmss") ?? string.Empty;
            return $"{path}|{stamp}";
        }

        return session.Version;
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
}
