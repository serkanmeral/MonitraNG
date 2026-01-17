using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngLLM.Application.Commands.Chat;
using MngLLM.Application.DTOs;
using MngLLM.Domain.Interfaces;

namespace MngLLM.Api.Controllers;

/// <summary>
/// Chatbot Controller - Moni chatbot için
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/chatbot")]
[Produces("application/json")]
public class ChatbotController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IContextManager _contextManager;
    private readonly ILogger<ChatbotController> _logger;

    public ChatbotController(
        IMediator mediator,
        IContextManager contextManager,
        ILogger<ChatbotController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Send message to chatbot
    /// </summary>
    /// <param name="request">Chat request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chat response</returns>
    [HttpPost("chat")]
    [Authorize(Policy = "AllowAnonymousInDevelopment")]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChatAsync(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Generate session ID if not provided
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();

            // Get conversation history from context manager
            var conversationHistory = _contextManager.GetConversationHistory(sessionId);
            var historyDtos = conversationHistory.Select(m => new ChatMessageDto
            {
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp
            }).ToList();

            var command = new ChatCommand
            {
                Message = request.Message,
                SessionId = sessionId,
                Language = request.Language,
                ConversationHistory = historyDtos
            };

            var response = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation(
                "Chat request processed: SessionId={SessionId}, Intent={Intent}, Language={Language}",
                response.SessionId, response.Intent, request.Language);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "An error occurred while processing your message", message = ex.Message });
        }
    }

    /// <summary>
    /// Clear conversation session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response</returns>
    [HttpDelete("session/{sessionId}")]
    [Authorize(Policy = "AllowAnonymousInDevelopment")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ClearSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest(new { error = "SessionId is required" });
        }

        try
        {
            _contextManager.ClearSession(sessionId);
            _logger.LogInformation("Session cleared: {SessionId}", sessionId);
            return Ok(new { message = "Session cleared successfully", sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing session: {SessionId}", sessionId);
            return StatusCode(500, new { error = "An error occurred while clearing session" });
        }
    }
}
