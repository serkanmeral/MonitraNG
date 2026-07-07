using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.EditorSessions;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>Collabora WOPI editör oturumları — sayım, limit ve kapanış.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/editor-sessions")]
[Authorize]
public sealed class EditorSessionsController : ControllerBase
{
    private readonly IEditorSessionService _sessions;
    private readonly IRequestContext _ctx;

    public EditorSessionsController(IEditorSessionService sessions, IRequestContext ctx)
    {
        _sessions = sessions;
        _ctx = ctx;
    }

    /// <summary>Aktif editör oturumu istatistikleri.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(EditorSessionStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var includeDetails = _ctx.IsAdmin || _ctx.IsManager;
        return Ok(await _sessions.GetStatsAsync(includeDetails, ct));
    }

    /// <summary>UI kapanışında oturumu sonlandırır (sahip, manager veya admin).</summary>
    [HttpPost("{token}/end")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult EndSession(string token)
    {
        _sessions.EndSession(token);
        return NoContent();
    }

    /// <summary>Oturumu zorla kapat (sahip, manager veya admin).</summary>
    [HttpDelete("{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult RevokeSession(string token)
    {
        _sessions.EndSession(token);
        return NoContent();
    }
}
