using MediatR;
using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Features.Directory.Commands.SyncDirectory;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers;

/// <summary>
/// Keycloak → Mongo directory sync (K2). DataGateway sync endpoint'lerinden ayrıdır.
/// </summary>
[ApiController]
[Route("api/directory")]
public class DirectorySyncController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DirectorySyncController> _logger;

    public DirectorySyncController(
        IMediator mediator,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DirectorySyncController> logger)
    {
        _mediator = mediator;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Tek domain için tam Keycloak → Mongo senkronu.
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(DirectorySyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DirectorySyncResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DirectorySyncResult>> Sync([FromBody] DirectorySyncRequest? request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var remoteIp = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var claims = httpContext?.Items["TokenClaims"] as TokenClaims;
        var domainId = request?.DomainId?.Trim();
        if (string.IsNullOrEmpty(domainId))
            domainId = claims?.DomainId;

        if (string.IsNullOrEmpty(domainId))
        {
            _logger.LogWarning(
                "[DirectorySync] POST /api/directory/sync rejected — missing domainId (remote={RemoteIp})",
                remoteIp);
            return BadRequest(new DirectorySyncResult
            {
                IsSuccess = false,
                Code = "invalid_request",
                Message = "domainId is required: Mongo ObjectId, domain name, or realm name (e.g. odak)."
            });
        }

        var trigger = request?.TriggeredBy ?? DirectorySyncTrigger.Manual;
        _logger.LogInformation(
            "[DirectorySync] POST /api/directory/sync received domain={DomainId} trigger={Trigger} remote={RemoteIp}",
            domainId, trigger, remoteIp);

        var result = await _mediator.Send(new SyncDirectoryCommand
        {
            DomainId = domainId,
            TriggeredBy = trigger
        });

        if (result.Code == "sync_in_progress")
        {
            _logger.LogInformation(
                "[DirectorySync] POST /api/directory/sync → 409 domain={DomainId} trigger={Trigger} (sync already running)",
                result.DomainId ?? domainId, trigger);
            return Conflict(result);
        }

        if (!result.IsSuccess && result.Code != "success")
        {
            var status = result.Code == "domain_not_found" ? 404 : 500;
            _logger.LogWarning(
                "[DirectorySync] POST /api/directory/sync → {HttpStatus} domain={DomainId} realm={Realm} code={Code} message={Message}",
                status, result.DomainId, result.RealmName, result.Code, result.Message);
            return StatusCode(status, result);
        }

        _logger.LogInformation(
            "[DirectorySync] POST /api/directory/sync → 200 domain={DomainId} realm={Realm} trigger={Trigger} " +
            "users +{UsersCreated}/~{UsersUpdated} groups +{GroupsCreated}/~{GroupsUpdated} deactivated={UsersDeactivated} ms={DurationMs}",
            result.DomainId, result.RealmName, trigger,
            result.UsersCreated, result.UsersUpdated,
            result.GroupsCreated, result.GroupsUpdated,
            result.UsersDeactivated, result.DurationMs);

        return Ok(result);
    }
}
