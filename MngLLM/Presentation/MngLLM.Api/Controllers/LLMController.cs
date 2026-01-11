using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngLLM.Application.Commands.TranslateText;
using MngLLM.Application.DTOs;

namespace MngLLM.Api.Controllers;

/// <summary>
/// LLM Service Controller
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/llm")]
[Authorize]
[Produces("application/json")]
public class LLMController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<LLMController> _logger;
    
    public LLMController(
        IMediator mediator,
        ILogger<LLMController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Translate text to multiple languages
    /// </summary>
    /// <param name="request">Translation request</param>
    /// <returns>Translation response</returns>
    [HttpPost("translate")]
    [ProducesResponseType(typeof(TranslationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TranslationResponseDto>> TranslateAsync([FromBody] TranslationRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { error = "Text is required" });
            }
            
            if (request.TargetLanguages == null || request.TargetLanguages.Count == 0)
            {
                return BadRequest(new { error = "TargetLanguages is required" });
            }
            
            var command = new TranslateTextCommand
            {
                Text = request.Text,
                SourceLanguage = request.SourceLanguage,
                TargetLanguages = request.TargetLanguages
            };
            
            var result = await _mediator.Send(command);
            
            _logger.LogInformation(
                "Translation completed: SourceLang={SourceLang}, TargetLangs={TargetLangs}, TextLength={TextLength}",
                request.SourceLanguage, string.Join(",", request.TargetLanguages), request.Text.Length);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error translating text");
            return StatusCode(500, new { error = "Translation failed", message = ex.Message });
        }
    }
}
