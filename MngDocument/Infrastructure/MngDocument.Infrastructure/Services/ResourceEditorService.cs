using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class ResourceEditorService : IResourceEditorService
{
    private static readonly HashSet<string> DocxExtensions = new(StringComparer.OrdinalIgnoreCase) { "docx" };
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IWopiSessionStore _sessions;
    private readonly IPermissionService _perms;
    private readonly MngDocumentSettings _settings;

    public ResourceEditorService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IWopiSessionStore sessions,
        IPermissionService perms,
        IOptions<MngDocumentSettings> settings)
    {
        _dg = dg;
        _ctx = ctx;
        _sessions = sessions;
        _perms = perms;
        _settings = settings.Value;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<ResourceEditorSessionDto> CreateEditorSessionAsync(string resourceId, CancellationToken ct = default)
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
        EnsureDocxFile(resource);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        var effective = snapshot.Resolve(resource);
        var readOnly = !effective.CanEdit;

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        var userName = _ctx.Username ?? userId;

        var session = new WopiSession
        {
            TemplateId = string.Empty,
            ResourceId = id,
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

        return new ResourceEditorSessionDto
        {
            ResourceId = id,
            EditorUrl = editorUrl,
            AccessToken = accessToken,
            WopiSrc = wopiSrc,
            ReadOnly = readOnly
        };
    }

    public async Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureDocxFile(resource);

        var fileName = ResolveFileName(resource);
        var bytes = await GetDocxBytesAsync(resource, session.DataGatewayToken, ct);

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
            UserCanRename = false
        };
    }

    public async Task<byte[]> GetFileContentsAsync(
        string resourceId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureDocxFile(resource);
        return await GetDocxBytesAsync(resource, session.DataGatewayToken, ct);
    }

    public async Task SaveFileContentsAsync(
        string resourceId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default)
    {
        EnsureResourceSession(resourceId, session);
        if (session.ReadOnly)
            throw DocumentException.Validation("READ_ONLY", "File is read-only.", "Dosya salt okunur.");

        var resource = await LoadOrThrowAsync(resourceId, session.DataGatewayToken, ct);
        EnsureDocxFile(resource);

        var fileName = ResolveFileName(resource);
        var filePayload = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(content),
            ["originalFileName"] = fileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["file"] = filePayload,
            ["size"] = content.LongLength,
            ["mimeType"] = DocxMime,
            ["extension"] = "docx"
        };

        await _dg.UpdateAsync<DmResource>(
            DmDatasets.Resources,
            resourceId,
            payload,
            session.DataGatewayToken,
            ct);

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

    private static void EnsureResourceSession(string resourceId, WopiSession session)
    {
        if (string.IsNullOrWhiteSpace(session.ResourceId)
            || !string.IsNullOrWhiteSpace(session.LetterheadId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.Equals(resourceId, session.ResourceId, StringComparison.Ordinal))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");
    }

    private static void EnsureDocxFile(DmResource resource)
    {
        if (!string.Equals(resource.type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "NOT_FILE",
                "Resource is not a file.",
                "Kaynak bir dosya değil.");
        }

        var ext = (resource.extension ?? string.Empty).Trim().TrimStart('.');
        var mime = resource.mimeType ?? string.Empty;
        var isDocx = DocxExtensions.Contains(ext)
            || mime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);

        if (!isDocx)
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_FILE_TYPE",
                "Only DOCX files can be opened in the editor.",
                "Editörde yalnızca DOCX dosyaları açılabilir.");
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

    private async Task<byte[]> GetDocxBytesAsync(DmResource resource, string token, CancellationToken ct)
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

        var name = resource.name ?? resource.title ?? "document.docx";
        if (!name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            name += ".docx";
        return name;
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
