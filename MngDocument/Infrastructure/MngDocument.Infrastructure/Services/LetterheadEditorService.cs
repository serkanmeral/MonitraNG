using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Helpers;

namespace MngDocument.Infrastructure.Services;

public sealed class LetterheadEditorService : ILetterheadEditorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IWopiSessionStore _sessions;
    private readonly ILetterheadService _letterheads;
    private readonly IDomainLogoProvider _logoProvider;
    private readonly ILetterheadFooterApplier _footerPreview;
    private readonly MngDocumentSettings _settings;

    public LetterheadEditorService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IWopiSessionStore sessions,
        ILetterheadService letterheads,
        IDomainLogoProvider logoProvider,
        ILetterheadFooterApplier footerPreview,
        IOptions<MngDocumentSettings> settings)
    {
        _dg = dg;
        _ctx = ctx;
        _sessions = sessions;
        _letterheads = letterheads;
        _logoProvider = logoProvider;
        _footerPreview = footerPreview;
        _settings = settings.Value;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<LetterheadDesignSessionDto> CreateDesignSessionAsync(string letterheadId, CancellationToken ct = default)
    {
        EnsureCollaboraEnabled();

        var id = letterheadId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        if (string.IsNullOrWhiteSpace(Token))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        _ = await _letterheads.GetByIdAsync(id, ct);
        await EnsureDesignFileAsync(id, ct);

        var dto = await _letterheads.GetByIdAsync(id, ct);
        var row = await LoadLetterheadAsync(id, Token!, ct);
        var rawDesignDocx = await GetDesignDocxBytesAsync(row, Token!, ct);
        var footerPreview = BuildFooterPreview(dto, rawDesignDocx);

        var userId = _ctx.UserId ?? _ctx.Username ?? "anonymous";
        var userName = _ctx.Username ?? userId;

        var session = new WopiSession
        {
            TemplateId = string.Empty,
            LetterheadId = id,
            UserId = userId,
            UserName = userName,
            DataGatewayToken = Token,
            Version = "1",
            ReadOnly = false
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
            .Append("&permission=edit")
            .Append("&lang=tr")
            .Append("&ui_defaults=")
            .Append(WebUtility.UrlEncode("UIMode=compact;TextSidebar=true;TextStatusbar=false"))
            .ToString();

        return new LetterheadDesignSessionDto
        {
            LetterheadId = id,
            EditorUrl = editorUrl,
            AccessToken = accessToken,
            WopiSrc = wopiSrc,
            ReadOnly = false,
            DesignFooterSource = footerPreview.Source,
            FooterPreviewLines = footerPreview.PreviewLines
        };
    }

    public async Task<WopiCheckFileInfoDto> GetCheckFileInfoAsync(
        string letterheadId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureLetterheadSession(letterheadId, session);
        var row = await LoadLetterheadAsync(letterheadId, session.DataGatewayToken, ct);
        var fileName = ResolveDesignFileName(row);
        var bytes = await GetDesignDocxBytesAsync(row, session.DataGatewayToken, ct);
        var branded = await PrepareDesignDocxForEditorAsync(row, bytes, session.DataGatewayToken, ct);

        return new WopiCheckFileInfoDto
        {
            BaseFileName = fileName,
            Size = branded.LongLength,
            OwnerId = session.UserId,
            UserId = session.UserId,
            UserFriendlyName = session.UserName,
            Version = session.Version,
            SupportsUpdate = true,
            UserCanWrite = true,
            UserCanNotWriteRelative = false,
            SupportsLocks = false,
            SupportsRename = false,
            UserCanRename = false
        };
    }

    public async Task<byte[]> GetFileContentsAsync(
        string letterheadId,
        WopiSession session,
        CancellationToken ct = default)
    {
        EnsureLetterheadSession(letterheadId, session);
        var row = await LoadLetterheadAsync(letterheadId, session.DataGatewayToken, ct);
        var bytes = await GetDesignDocxBytesAsync(row, session.DataGatewayToken, ct);
        return await PrepareDesignDocxForEditorAsync(row, bytes, session.DataGatewayToken, ct);
    }

    public async Task SaveFileContentsAsync(
        string letterheadId,
        WopiSession session,
        byte[] content,
        string? accessToken,
        CancellationToken ct = default)
    {
        EnsureLetterheadSession(letterheadId, session);
        var row = await LoadLetterheadAsync(letterheadId, session.DataGatewayToken, ct);
        var fileName = ResolveDesignFileName(row);

        var designFile = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(content),
            ["originalFileName"] = fileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["name"] = row.name,
            ["code"] = row.code,
            ["description"] = row.description,
            ["isDefault"] = row.isDefault,
            ["isActive"] = row.isActive,
            ["letterheadJson"] = row.letterheadJson,
            ["settingsJson"] = row.settingsJson,
            ["designFileName"] = fileName,
            ["designFile"] = designFile,
            ["updatedBy"] = session.UserName,
            ["updatedAt"] = DateTime.UtcNow
        };

        var updated = await _dg.UpdateAsync<DmLetterhead>(
            DmDatasets.Letterheads,
            letterheadId,
            payload,
            session.DataGatewayToken,
            ct);

        var (path, storedName) = DgFileFieldReader.Read(updated);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await _dg.UpdateAsync<DmLetterhead>(
                DmDatasets.Letterheads,
                letterheadId,
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

        var newVersion = (long.TryParse(session.Version, out var current) ? current + 1 : 2).ToString();
        if (!string.IsNullOrWhiteSpace(accessToken))
            _sessions.BumpVersion(accessToken, newVersion);
        else
            session.Version = newVersion;
    }

    private async Task EnsureDesignFileAsync(string letterheadId, CancellationToken ct)
    {
        var row = await LoadLetterheadAsync(letterheadId, Token, ct);
        if (!string.IsNullOrWhiteSpace(row.designStoragePath))
            return;

        var (pathFromField, _) = DgFileFieldReader.Read(row);
        if (!string.IsNullOrWhiteSpace(pathFromField))
            return;

        var dto = await _letterheads.GetByIdAsync(letterheadId, ct);
        var skeleton = await BuildSkeletonDocxAsync(dto, ct);
        var fileName = ResolveDesignFileName(row);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = row.name,
            ["code"] = row.code,
            ["description"] = row.description,
            ["isDefault"] = row.isDefault,
            ["isActive"] = row.isActive,
            ["letterheadJson"] = row.letterheadJson,
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

        var updated = await _dg.UpdateAsync<DmLetterhead>(
            DmDatasets.Letterheads,
            letterheadId,
            payload,
            Token,
            ct);

        var (path, storedName) = DgFileFieldReader.Read(updated);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw DocumentException.Validation(
                "LETTERHEAD_DESIGN_INIT_FAILED",
                "Letterhead design file could not be initialized.",
                "Antet tasarım dosyası oluşturulamadı.");
        }

        await _dg.UpdateAsync<DmLetterhead>(
            DmDatasets.Letterheads,
            letterheadId,
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

    private async Task<byte[]> BuildSkeletonDocxAsync(LetterheadDto dto, CancellationToken ct)
    {
        var settings = LetterheadSettingsSerializer.Normalize(dto.Settings);
        var baseModel = TemplateModelSerializer.ToLetterheadModel(dto.Letterhead) ?? new TemplateLetterheadModel { Enabled = true };
        var letterhead = LetterheadSettingsSerializer.ApplyHeaderFields(baseModel, settings.HeaderFields);
        if (!letterhead.Enabled)
            return LetterheadDesignSkeletonBuilder.Build(MinimalDocxFactory.CreateBlank(), settings, letterhead, dto.Name, null, ".png");

        DomainLogoResult? logo = null;
        if (letterhead.ShowLogo)
            logo = await _logoProvider.GetCurrentDomainLogoAsync(Token, ct);

        return LetterheadDesignSkeletonBuilder.Build(
            MinimalDocxFactory.CreateBlank(),
            settings,
            letterhead,
            dto.Name,
            logo?.Bytes,
            logo?.Extension ?? ".png");
    }

    private async Task<byte[]> PrepareDesignDocxForEditorAsync(
        DmLetterhead row,
        byte[] rawDocxBytes,
        string token,
        CancellationToken ct)
    {
        var dto = MapRowToDto(row);
        var settings = LetterheadSettingsSerializer.Normalize(dto.Settings);
        var baseModel = TemplateModelSerializer.ToLetterheadModel(dto.Letterhead) ?? new TemplateLetterheadModel { Enabled = true };
        var letterhead = LetterheadSettingsSerializer.ApplyHeaderFields(baseModel, settings.HeaderFields);

        DomainLogoResult? logo = null;
        if (letterhead.ShowLogo)
            logo = await _logoProvider.GetCurrentDomainLogoAsync(token, ct);

        return LetterheadDesignSkeletonBuilder.EnsureEditorParts(
            rawDocxBytes,
            settings,
            letterhead,
            dto.Name,
            logo?.Bytes,
            logo?.Extension ?? ".png");
    }

    private static LetterheadDto MapRowToDto(DmLetterhead row)
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
            DesignFileName = row.designFileName ?? designNameFromField,
            HasDesign = !string.IsNullOrWhiteSpace(designStoragePath),
            CreatedBy = row.createdBy,
            CreatedAt = row.createdAt,
            UpdatedAt = row.updatedAt
        };
    }

    private (string Source, IReadOnlyList<string> PreviewLines) BuildFooterPreview(
        LetterheadDto dto,
        byte[] rawDesignDocx)
    {
        var settings = LetterheadSettingsSerializer.Normalize(dto.Settings);
        return _footerPreview.DescribePreview(settings, rawDesignDocx);
    }

    private static void EnsureLetterheadSession(string letterheadId, WopiSession session)
    {
        if (string.IsNullOrWhiteSpace(session.LetterheadId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.IsNullOrWhiteSpace(session.ResourceId)
            || !string.IsNullOrWhiteSpace(session.TemplateId))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");

        if (!string.Equals(letterheadId, session.LetterheadId, StringComparison.Ordinal))
            throw DocumentException.NotFound("WOPI oturumu geçersiz.");
    }

    private async Task<DmLetterhead> LoadLetterheadAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<DmLetterhead>(DmDatasets.Letterheads, id, token, ct);
        if (row is null || row.__dataId is null)
            throw DocumentException.NotFound("Antet bulunamadı.");
        return row;
    }

    private async Task<byte[]> GetDesignDocxBytesAsync(DmLetterhead row, string token, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(row.designStoragePath))
            return await _dg.DownloadFileAsync(row.designStoragePath, token, ct);

        var (path, _) = DgFileFieldReader.Read(row);
        if (!string.IsNullOrWhiteSpace(path))
            return await _dg.DownloadFileAsync(path!, token, ct);

        throw DocumentException.Validation(
            "LETTERHEAD_DESIGN_MISSING",
            "Letterhead design file is missing.",
            "Antet tasarım dosyası bulunamadı.");
    }

    private static string ResolveDesignFileName(DmLetterhead row)
    {
        if (!string.IsNullOrWhiteSpace(row.designFileName))
            return row.designFileName!;

        var code = row.code?.Trim();
        return string.IsNullOrWhiteSpace(code) ? "letterhead-design.docx" : $"{code}-design.docx";
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
