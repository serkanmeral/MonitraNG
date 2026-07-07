using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Collabora WOPI host — JWT yerine <c>access_token</c> query ile oturum doğrulama.</summary>
[ApiController]
[AllowAnonymous]
[Route("wopi/files")]
public sealed class WopiController : ControllerBase
{
    private static readonly JsonSerializerOptions WopiJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ITemplateEditorService _editor;
    private readonly IResourceEditorService _resourceEditor;
    private readonly ILetterheadEditorService _letterheadEditor;
    private readonly IWopiSessionStore _sessions;

    public WopiController(
        ITemplateEditorService editor,
        IResourceEditorService resourceEditor,
        ILetterheadEditorService letterheadEditor,
        IWopiSessionStore sessions)
    {
        _editor = editor;
        _resourceEditor = resourceEditor;
        _letterheadEditor = letterheadEditor;
        _sessions = sessions;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> CheckFileInfo(string id, [FromQuery] string access_token, CancellationToken ct)
    {
        var session = ResolveSession(access_token);
        if (session is null)
            return Unauthorized();

        var info = !string.IsNullOrWhiteSpace(session.ResourceId)
            ? await _resourceEditor.GetCheckFileInfoAsync(id, session, ct)
            : !string.IsNullOrWhiteSpace(session.LetterheadId)
                ? await _letterheadEditor.GetCheckFileInfoAsync(id, session, ct)
                : await _editor.GetCheckFileInfoAsync(id, session, ct);
        return Content(JsonSerializer.Serialize(info, WopiJsonOptions), "application/json");
    }

    [HttpGet("{id}/contents")]
    public async Task<IActionResult> GetFile(string id, [FromQuery] string access_token, CancellationToken ct)
    {
        var session = ResolveSession(access_token);
        if (session is null)
            return Unauthorized();

        var bytes = !string.IsNullOrWhiteSpace(session.ResourceId)
            ? await _resourceEditor.GetFileContentsAsync(id, session, ct)
            : !string.IsNullOrWhiteSpace(session.LetterheadId)
                ? await _letterheadEditor.GetFileContentsAsync(id, session, ct)
                : await _editor.GetFileContentsAsync(id, session, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    [HttpPost("{id}/contents")]
    public async Task<IActionResult> PutFile(string id, [FromQuery] string access_token, CancellationToken ct)
    {
        var session = ResolveSession(access_token);
        if (session is null)
            return Unauthorized();

        await using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var content = ms.ToArray();

        var versionBefore = session.Version;
        if (!string.IsNullOrWhiteSpace(session.ResourceId))
            await _resourceEditor.SaveFileContentsAsync(id, session, content, access_token, ct);
        else if (!string.IsNullOrWhiteSpace(session.LetterheadId))
            await _letterheadEditor.SaveFileContentsAsync(id, session, content, access_token, ct);
        else
            await _editor.SaveFileContentsAsync(id, session, content, access_token, ct);

        var refreshed = _sessions.GetSession(access_token);
        var version = refreshed?.Version ?? versionBefore;

        Response.Headers["X-WOPI-ItemVersion"] = version;
        return Ok();
    }

    private WopiSession? ResolveSession(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var session = _sessions.GetSession(accessToken);
        if (session is not null)
            _sessions.Touch(accessToken);

        return session;
    }
}
