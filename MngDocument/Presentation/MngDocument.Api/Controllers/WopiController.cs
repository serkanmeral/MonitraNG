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
    private readonly IWopiSessionStore _sessions;

    public WopiController(ITemplateEditorService editor, IWopiSessionStore sessions)
    {
        _editor = editor;
        _sessions = sessions;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> CheckFileInfo(string id, [FromQuery] string access_token, CancellationToken ct)
    {
        var session = ResolveSession(access_token);
        if (session is null)
            return Unauthorized();

        var info = await _editor.GetCheckFileInfoAsync(id, session, ct);
        return Content(JsonSerializer.Serialize(info, WopiJsonOptions), "application/json");
    }

    [HttpGet("{id}/contents")]
    public async Task<IActionResult> GetFile(string id, [FromQuery] string access_token, CancellationToken ct)
    {
        var session = ResolveSession(access_token);
        if (session is null)
            return Unauthorized();

        var bytes = await _editor.GetFileContentsAsync(id, session, ct);
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
        await _editor.SaveFileContentsAsync(id, session, content, access_token, ct);

        var refreshed = _sessions.GetSession(access_token);
        var version = refreshed?.Version ?? versionBefore;

        Response.Headers["X-WOPI-ItemVersion"] = version;
        return Ok();
    }

    private WopiSession? ResolveSession(string? accessToken) =>
        string.IsNullOrWhiteSpace(accessToken) ? null : _sessions.GetSession(accessToken);
}
