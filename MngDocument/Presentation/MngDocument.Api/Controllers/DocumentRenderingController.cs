using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

/// <summary>On-prem DOCX→PDF altyapısı (Gotenberg / headless LibreOffice).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/rendering")]
[Authorize]
public sealed class DocumentRenderingController : ControllerBase
{
    private readonly IDocumentRenderService _rendering;

    public DocumentRenderingController(IDocumentRenderService rendering)
    {
        _rendering = rendering;
    }

    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct) =>
        Ok(await _rendering.GetStatusAsync(ct));
}
