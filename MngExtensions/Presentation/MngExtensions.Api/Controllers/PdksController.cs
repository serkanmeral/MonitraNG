using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngExtensions.Api.Pdks;
using MngExtensions.Application.Pdks;

namespace MngExtensions.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/pdks")]
public class PdksController : ControllerBase
{
    private readonly IPdksService _pdks;

    public PdksController(IPdksService pdks)
    {
        _pdks = pdks;
    }

    [HttpGet("doors/{doorKey}/code")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueCode(string doorKey, [FromQuery(Name = "k")] string? kioskToken, CancellationToken cancellationToken)
    {
        var result = await _pdks.IssueCodeAsync(doorKey, kioskToken, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });

        return Ok(new
        {
            nonce = result.Nonce,
            url = result.Url,
            expiresAt = result.ExpiresAt,
            doorTitle = result.DoorTitle,
            welcome = result.Welcome,
            boardStamp = result.BoardStamp
        });
    }

    [HttpGet("doors/{doorKey}/board")]
    [AllowAnonymous]
    public async Task<IActionResult> Board(string doorKey, [FromQuery(Name = "k")] string? kioskToken, CancellationToken cancellationToken)
    {
        var result = await _pdks.GetBoardAsync(doorKey, kioskToken, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });

        return Ok(new
        {
            title = result.Title,
            cards = result.Cards,
            day = result.Day,
            breaks = result.Breaks,
            refreshSeconds = result.RefreshSeconds
        });
    }

    [HttpGet("report")]
    [Authorize]
    public async Task<IActionResult> Report([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? locationKey, CancellationToken cancellationToken)
    {
        var result = await _pdks.GetReportAsync(PdksClaims.ReadActor(User), from, to, locationKey, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });

        return Ok(new
        {
            from = result.From,
            to = result.To,
            locationKey = result.LocationKey,
            people = result.People
        });
    }

    [HttpGet("live")]
    [Authorize]
    public async Task<IActionResult> Live([FromQuery] string? locationKey, [FromQuery] long? stamp, CancellationToken cancellationToken)
    {
        var result = await _pdks.GetLiveAsync(PdksClaims.ReadActor(User), locationKey, stamp, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });
        if (result.Unchanged)
            return Ok(new { stamp = result.Stamp, unchanged = true, locationKey = result.LocationKey });

        return Ok(new
        {
            stamp = result.Stamp,
            unchanged = false,
            locationKey = result.LocationKey,
            cards = result.Cards,
            inside = result.Inside,
            breaks = result.Breaks,
            left = result.Left
        });
    }

    [HttpGet("locations")]
    [Authorize]
    public async Task<IActionResult> Locations(CancellationToken cancellationToken)
    {
        var result = await _pdks.ListLocationsAsync(PdksClaims.ReadActor(User), cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });
        return Ok(result.Items);
    }

    [HttpGet("locations/{locationKey}/doors")]
    [Authorize]
    public async Task<IActionResult> Doors(string locationKey, CancellationToken cancellationToken)
    {
        var result = await _pdks.ListDoorsAsync(PdksClaims.ReadActor(User), locationKey, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });
        return Ok(result.Items);
    }

    [HttpPost("doors/{doorKey}/bind")]
    [Authorize]
    public async Task<IActionResult> Bind(string doorKey, CancellationToken cancellationToken)
    {
        var result = await _pdks.BindScreenAsync(PdksClaims.ReadActor(User), doorKey, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });
        return Ok(new
        {
            doorKey = result.DoorKey,
            doorTitle = result.DoorTitle,
            locationKey = result.LocationKey,
            locationTitle = result.LocationTitle,
            kioskToken = result.KioskToken
        });
    }

    [HttpPost("passages/hold")]
    [AllowAnonymous]
    public async Task<IActionResult> Hold([FromBody] ConsumeRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pdks.HoldAsync(request?.Nonce, cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message });

        return Ok(new { status = result.Status, expiresAt = result.ExpiresAt });
    }

    [HttpPost("passages/preview")]
    [Authorize]
    public async Task<IActionResult> Preview([FromBody] MobilePassageRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pdks.PreviewAsync(
            request?.Nonce,
            request?.PublicKey,
            Request.Headers.Authorization.ToString(),
            PdksClaims.ReadActor(User),
            cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message, status = result.Status });

        return Ok(new
        {
            status = result.Status,
            message = result.Message,
            locationTitle = result.LocationTitle,
            doorTitle = result.DoorTitle,
            personName = result.PersonName,
            direction = result.Direction,
            breakAction = result.BreakAction,
            photoRequired = result.PhotoRequired
        });
    }

    [HttpPost("passages/confirm")]
    [Authorize]
    public async Task<IActionResult> Confirm([FromBody] MobilePassageRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pdks.ConfirmAsync(
            request?.Nonce,
            request?.PublicKey,
            request?.Signature,
            request?.Photo,
            Request.Headers.Authorization.ToString(),
            PdksClaims.ReadActor(User),
            cancellationToken,
            request?.Action);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message, result = result.Result, rejectReason = result.RejectReason });

        return Ok(new
        {
            result = result.Result,
            rejectReason = result.RejectReason,
            direction = result.Direction,
            occurredAt = result.OccurredAt,
            displayName = result.DisplayName,
            doorKey = result.DoorKey,
            doorTitle = result.DoorTitle,
            greeting = result.Greeting,
            message = result.Message
        });
    }

    [HttpPost("passages")]
    [Authorize]
    public async Task<IActionResult> Consume([FromBody] ConsumeRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pdks.ConsumeAsync(request?.Nonce, PdksClaims.ReadActor(User), cancellationToken);
        if (result.StatusCode != StatusCodes.Status200OK)
            return StatusCode(result.StatusCode, new { message = result.Message, result = result.Result });

        return Ok(new
        {
            result = result.Result,
            rejectReason = result.RejectReason,
            direction = result.Direction,
            occurredAt = result.OccurredAt,
            displayName = result.DisplayName,
            doorKey = result.DoorKey,
            doorTitle = result.DoorTitle
        });
    }

    public sealed class ConsumeRequest
    {
        public string? Nonce { get; set; }
    }

    public sealed class MobilePassageRequest
    {
        public string? Nonce { get; set; }
        public string? PublicKey { get; set; }
        public string? Signature { get; set; }
        public string? Photo { get; set; }
        public string? Action { get; set; }
    }
}
