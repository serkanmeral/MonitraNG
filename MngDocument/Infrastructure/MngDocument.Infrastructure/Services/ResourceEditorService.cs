using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.EditorSessions;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class ResourceEditorService : IResourceEditorService
{

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IWopiSessionStore _sessions;
    private readonly IEditorSessionService _editorSessions;
    private readonly IPermissionService _perms;
    private readonly IResourceService _resources;
    private readonly MngDocumentSettings _settings;

    public ResourceEditorService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IWopiSessionStore sessions,
        IEditorSessionService editorSessions,
        IPermissionService perms,
        IResourceService resources,
        IOptions<MngDocumentSettings> settings)
    {
        _dg = dg;
        _ctx = ctx;
        _sessions = sessions;
        _editorSessions = editorSessions;
        _perms = perms;
        _resources = resources;
        _settings = settings.Value;
    }

    private string? Token => _ctx.BearerToken;

    public DocumentEditorLockStatusDto GetEditorLockStatus(string resourceId)
    {
        var id = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        return _editorSessions.GetDocumentLockStatus(id, null, null, userId, _ctx.IsAdmin || _ctx.IsManager);
    }

    public async Task<ResourceEditorSessionDto> CreateEditorSessionAsync(
        string resourceId,
        bool? requestReadOnly = null,
        bool bypassLock = false,
        string? postMessageOrigin = null,
        CancellationToken ct = default)
    {
        EnsureCollaboraEnabled();

        var id = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        var resource = await LoadOrThrowAsync(id, ct);
        EnsureManagedOfficeFile(resource);
        EnsureManagedDocument(resource);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        var effective = snapshot.Resolve(resource);
        var canEdit = effective.CanEdit;
        var wantReadOnly = requestReadOnly ?? !canEdit;
        var readOnly = wantReadOnly || !canEdit;

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        var userName = _ctx.Username ?? userId;

        var lockStatus = _editorSessions.GetDocumentLockStatus(id, null, null, userId, _ctx.IsAdmin || _ctx.IsManager);
        var locked = lockStatus.IsLocked;
        var lockEnforced = false;

        if (locked && canEdit)
        {
            var canBypass = lockStatus.CanBypassLock && bypassLock;
            if (lockStatus.EnforceExclusiveLock && !canBypass && !wantReadOnly)
            {
                readOnly = true;
                lockEnforced = true;
            }
        }

        var session = new WopiSession
        {
            TemplateId = string.Empty,
            ResourceId = id,
            UserId = userId,
            UserName = userName,
            DataGatewayToken = Token,
            Version = (resource.currentVersionNumber ?? 1).ToString(),
            ReadOnly = readOnly,
            PostMessageOrigin = WopiCollaboraHelper.NormalizePostMessageOrigin(postMessageOrigin)
        };

        var ttl = TimeSpan.FromMinutes(Math.Clamp(_settings.Wopi.SessionMinutes, 15, 1440));
        var accessToken = _editorSessions.BeginSession(session, ttl);
        var (editorUrl, wopiSrc) = BuildEditorUrls(id, accessToken, readOnly);

        return new ResourceEditorSessionDto
        {
            ResourceId = id,
            EditorUrl = editorUrl,
            AccessToken = accessToken,
            WopiSrc = wopiSrc,
            ReadOnly = readOnly,
            LockedByOthers = lockStatus.IsLockedByOthers,
            LockEnforced = lockEnforced && readOnly
        };
    }

    public async Task<ResourceEditorSessionDto> CreateVersionPreviewSessionAsync(
        string resourceId,
        int versionNumber,
        CancellationToken ct = default)
    {
        EnsureCollaboraEnabled();

        var id = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        if (versionNumber <= 0)
        {
            throw DocumentException.Validation(
                "INVALID_VERSION",
                "Version number must be positive.",
                "Geçersiz sürüm numarası.");
        }

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        var resource = await LoadOrThrowAsync(id, ct);
        EnsureManagedOfficeFile(resource);
        EnsureManagedDocument(resource);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        var effective = snapshot.Resolve(resource);
        if (!effective.CanDownload)
            throw DocumentException.Forbidden("Önizleme için indirme yetkisi gerekir.");

        // Sürüm var mı (snapshot yoksa erken hata)?
        await _resources.GetFileVersionContentForEditorAsync(id, versionNumber, Token, ct);

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        var userName = _ctx.Username ?? userId;

        var session = new WopiSession
        {
            TemplateId = string.Empty,
            ResourceId = id,
            UserId = userId,
            UserName = userName,
            DataGatewayToken = Token,
            Version = versionNumber.ToString(),
            PreviewVersionNumber = versionNumber,
            ReadOnly = true
        };

        var ttl = TimeSpan.FromMinutes(Math.Clamp(_settings.Wopi.SessionMinutes, 15, 1440));
        var accessToken = _editorSessions.BeginSession(session, ttl);
        var (editorUrl, wopiSrc) = BuildEditorUrls(id, accessToken, readOnly: true);

        return new ResourceEditorSessionDto
        {
            ResourceId = id,
            EditorUrl = editorUrl,
            AccessToken = accessToken,
            WopiSrc = wopiSrc,
            ReadOnly = true
        };
    }

    public async Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureManagedOfficeFile(resource);

        var fileName = ResolveFileName(resource);
        var bytes = await ResolveOfficeBytesAsync(resourceId, resource, session, ct);

        return new WopiCheckFileInfoDto
        {
            BaseFileName = fileName,
            Size = bytes.LongLength,
            OwnerId = session.UserId,
            UserId = session.UserId,
            UserFriendlyName = session.UserName,
            Version = session.Version,
            SupportsUpdate = !session.ReadOnly,
            UserCanWrite = !session.ReadOnly,
            UserCanNotWriteRelative = false,
            SupportsLocks = false,
            SupportsRename = false,
            UserCanRename = false,
            PostMessageOrigin = WopiCollaboraHelper.ResolvePostMessageOrigin(session, _settings.Collabora)
        };
    }

    public async Task<byte[]> GetFileContentsAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureManagedOfficeFile(resource);
        return await ResolveOfficeBytesAsync(resourceId, resource, session, ct);
    }

    public async Task<(byte[] Content, string ContentType)> GetFileWithContentTypeAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureManagedOfficeFile(resource);
        var profile = ResolveOfficeProfile(resource);
        var bytes = await ResolveOfficeBytesAsync(resourceId, resource, session, ct);
        return (bytes, profile.MimeType);
    }

    public async Task SaveFileContentsAsync(
        string resourceId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        if (session.ReadOnly || session.PreviewVersionNumber is not null)
            throw DocumentException.Validation("READ_ONLY", "File is read-only.", "Dosya salt okunur.");

        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureManagedOfficeFile(resource);

        var fileName = ResolveFileName(resource);
        var newVersion = await _resources.SaveManagedDocumentFileAsync(
            resourceId,
            content,
            fileName,
            session.DataGatewayToken,
            ct);

        var newVersionText = newVersion.ToString();
        if (!string.IsNullOrWhiteSpace(accessToken))
            _sessions.BumpVersion(accessToken, newVersionText);
        else
            session.Version = newVersionText;
    }

    private (string EditorUrl, string WopiSrc) BuildEditorUrls(string resourceId, string accessToken, bool readOnly)
    {
        var wopiHost = _settings.Wopi.HostBaseUrl.TrimEnd('/');
        var wopiSrc = $"{wopiHost}/wopi/files/{Uri.EscapeDataString(resourceId)}";

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

        return (editorUrl, wopiSrc);
    }

    private async Task<byte[]> ResolveOfficeBytesAsync(
        string resourceId,
        DmResource resource,
        WopiSession session,
        CancellationToken ct)
    {
        if (session.PreviewVersionNumber is int versionNumber)
        {
            var (bytes, _) = await _resources.GetFileVersionContentForEditorAsync(
                resourceId,
                versionNumber,
                session.DataGatewayToken,
                ct);
            return bytes;
        }

        return await GetOfficeBytesAsync(resource, session.DataGatewayToken, ct);
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

    private static void EnsureResourceSession(string resourceId, WopiSession session)
    {
        if (string.IsNullOrWhiteSpace(session.ResourceId)
            || !string.IsNullOrWhiteSpace(session.LetterheadId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.Equals(resourceId, session.ResourceId, StringComparison.Ordinal))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");
    }

    private static void EnsureManagedOfficeFile(DmResource resource)
    {
        if (!string.Equals(resource.type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "NOT_FILE",
                "Resource is not a file.",
                "Kaynak bir dosya değil.");
        }

        if (!ManagedOfficeProfiles.TryResolve(resource.extension, resource.mimeType, out _))
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_FILE_TYPE",
                "Only DOCX, XLSX and PPTX files can be opened in the editor.",
                "Editörde yalnızca DOCX, XLSX ve PPTX dosyaları açılabilir.");
        }
    }

    private static ManagedOfficeProfile ResolveOfficeProfile(DmResource resource)
    {
        if (!ManagedOfficeProfiles.TryResolve(resource.extension, resource.mimeType, out var profile))
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_FILE_TYPE",
                "Unsupported managed office file type.",
                "Desteklenmeyen Office dosya türü.");
        }

        return profile;
    }

    private static void EnsureManagedDocument(DmResource resource)
    {
        var origin = resource.origin?.Trim() ?? string.Empty;
        if (!ResourceOrigin.IsManagedDocument(origin))
        {
            throw DocumentException.Validation(
                "UPLOAD_NOT_EDITABLE",
                "Uploaded files cannot be opened in the editor.",
                "Yüklenen dosyalar editörde açılamaz.");
        }
    }

    private async Task<DmResource> LoadOrThrowAsync(string id, CancellationToken ct) =>
        await LoadOrThrowAsync(id, Token, ct);

    private async Task<DmResource> LoadOrThrowAsync(string id, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var resource = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, token, ct);
        if (resource is null || resource.__dataId is null)
            throw DocumentException.NotFound("Dosya bulunamadı.");

        return resource;
    }

    private async Task<byte[]> GetOfficeBytesAsync(DmResource resource, string token, CancellationToken ct)
    {
        var (path, _) = ReadFileField(resource.file);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw DocumentException.Validation(
                "FILE_MISSING",
                "File content is missing.",
                "Dosya içeriği bulunamadı.");
        }

        return await _dg.DownloadFileAsync(path, token, ct);
    }

    private static string ResolveFileName(DmResource resource)
    {
        var (_, fileName) = ReadFileField(resource.file);
        if (!string.IsNullOrWhiteSpace(fileName))
            return fileName!;

        var profile = ResolveOfficeProfile(resource);
        var name = resource.name ?? resource.title ?? profile.DefaultFileName;
        return ManagedOfficeProfiles.EnsureFileNameHasExtension(name, profile);
    }

    private static (string? Path, string? Name) ReadFileField(JsonElement? file)
    {
        if (file is null || file.Value.ValueKind != JsonValueKind.Object)
            return (null, null);

        string? path = file.Value.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
        string? name = file.Value.TryGetProperty("file_name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()
            : null;
        return (path, name);
    }
}
