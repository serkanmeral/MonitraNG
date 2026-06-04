using Microsoft.AspNetCore.Mvc;
using MngEngine.Application.Interfaces;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class SecEventsController : ControllerBase
{
    private readonly ISecEventFixtureReplay _fixtureReplay;
    private readonly ISecEventBatchQueue _queue;
    private readonly ISecEventSendProcessing _sendProcessing;

    public SecEventsController(
        ISecEventFixtureReplay fixtureReplay,
        ISecEventBatchQueue queue,
        ISecEventSendProcessing sendProcessing)
    {
        _fixtureReplay = fixtureReplay;
        _queue = queue;
        _sendProcessing = sendProcessing;
    }

    /// <summary>Syslog kuyruk özeti (tüketmeden).</summary>
    [HttpGet("queue")]
    public IActionResult GetQueue() =>
        Ok(new { count = _queue.Count });

    /// <summary>Kuyruktaki öğeleri hemen Reactor'a gönderir.</summary>
    [HttpPost("flush")]
    public async Task<IActionResult> FlushQueue(CancellationToken cancellationToken)
    {
        var result = await _sendProcessing.FlushAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new
            {
                error = "sec_event_flush_failed",
                message = result.ErrorMessage,
                result.Accepted,
                result.Rejected,
                result.Published
            });
        }

        return Ok(new
        {
            accepted = result.Accepted,
            rejected = result.Rejected,
            published = result.Published
        });
    }

    /// <summary>
    /// SIEM Faz 1 spike B — tests/fixtures/siem dosyalarını Reactor sec-events ingest'e gönderir.
    /// </summary>
    [HttpPost("replay-fixtures")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplayFixtures(CancellationToken cancellationToken)
    {
        var result = await _fixtureReplay.ReplayFixturesAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new
            {
                error = "sec_event_replay_failed",
                message = result.ErrorMessage,
                result.Accepted,
                result.Rejected,
                result.Published
            });
        }

        return Ok(new
        {
            accepted = result.Accepted,
            rejected = result.Rejected,
            published = result.Published
        });
    }
}
