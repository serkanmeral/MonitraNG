using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MngLogCollector.Api.Filters;
using MngLogCollector.Application.Abstractions.Ingest;
using MngLogCollector.Application.Contracts.Ingest;

namespace MngLogCollector.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ingest")]
[IngestApiKey]
public sealed class IngestController(IIngestBatchService ingest) : ControllerBase
{
    [HttpPost("batches")]
    [ProducesResponseType(typeof(IngestBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PostBatch(
        [FromBody] IngestBatchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await ingest.IngestAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
