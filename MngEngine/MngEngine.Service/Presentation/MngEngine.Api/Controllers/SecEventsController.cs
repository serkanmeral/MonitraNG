using Microsoft.AspNetCore.Mvc;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;

namespace MngEngine.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class SecEventsController : ControllerBase
{
    private readonly ISecEventFixtureReplay _fixtureReplay;
    private readonly ISecEventBatchQueue _queue;
    private readonly ISecEventSendProcessing _sendProcessing;
    private readonly ISecEventQueueIngestService _queueIngest;
    private readonly SecEventQueueOptions _queueOptions;

    public SecEventsController(
        ISecEventFixtureReplay fixtureReplay,
        ISecEventBatchQueue queue,
        ISecEventSendProcessing sendProcessing,
        ISecEventQueueIngestService queueIngest,
        Microsoft.Extensions.Options.IOptions<SecEventQueueOptions> queueOptions)
    {
        _fixtureReplay = fixtureReplay;
        _queue = queue;
        _sendProcessing = sendProcessing;
        _queueIngest = queueIngest;
        _queueOptions = queueOptions.Value;
    }

    /// <summary>Syslog kuyruk özeti (tüketmeden).</summary>
    [HttpGet("queue")]
    public IActionResult GetQueue() =>
        Ok(new { count = _queue.Count, maxItems = _queueOptions.MaxItems });

    /// <summary>
    /// WEF→WEC forwarder batch ingest. WEC tarafı Windows Event JSON push eder; Engine kuyruğa alır → Reactor.
    /// </summary>
    [HttpPost("wec-batch")]
    [ProducesResponseType(typeof(SecEventWecBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> IngestWecBatch(
        [FromBody] SecEventWecBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_queueOptions.WecIngestEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "wec_ingest_disabled",
                message = "MngEngine:SecEventQueue:WecIngestEnabled=false"
            });
        }

        if (request?.Items == null || request.Items.Count == 0)
            return BadRequest(new { error = "empty_batch", message = "items bos olamaz" });

        var maxBatch = _queueOptions.MaxWecBatchItems > 0 ? _queueOptions.MaxWecBatchItems : 500;
        if (request.Items.Count > maxBatch)
        {
            return BadRequest(new
            {
                error = "batch_too_large",
                message = $"items.Count ({request.Items.Count}) MaxWecBatchItems ({maxBatch}) ustunde",
                maxItems = maxBatch
            });
        }

        var response = await _queueIngest.IngestWecBatchAsync(request, cancellationToken);
        return Ok(response);
    }

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
