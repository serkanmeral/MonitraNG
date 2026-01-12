using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;

namespace MngNotifier.Api.Controllers;

/// <summary>
/// Notification controller for sending mail notifications
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[AllowAnonymous] // Direct API endpoint - no authentication required
public class NotificationController : ControllerBase
{
    private readonly IMailProvider _mailProvider;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(IMailProvider mailProvider, ILogger<NotificationController> logger)
    {
        _mailProvider = mailProvider ?? throw new ArgumentNullException(nameof(mailProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sends a mail notification (Direct API - No authentication required)
    /// </summary>
    /// <param name="request">Mail notification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification response with ID and status</returns>
    [HttpPost("mail")]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Basic validation
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (request.To == null || request.To.Count == 0)
            {
                return BadRequest(new { error = "At least one 'to' recipient is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                return BadRequest(new { error = "Subject is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new { error = "Body is required" });
            }

            // Generate notification ID (temporary - will use MongoDB ObjectId in future)
            var notificationId = Guid.NewGuid().ToString();

            // Send mail directly (synchronous for now - will be async with RabbitMQ in future)
            await _mailProvider.SendMailAsync(request, cancellationToken);

            _logger.LogInformation("Mail notification sent successfully. NotificationId: {NotificationId}, To: {To}, Subject: {Subject}", 
                notificationId, string.Join(", ", request.To), request.Subject);

            var response = new SendMailResponse
            {
                NotificationId = notificationId,
                Status = "sent", // For direct API, mail is sent immediately
                QueuedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail notification. To: {To}, Subject: {Subject}", 
                request?.To != null ? string.Join(", ", request.To) : "unknown", request?.Subject ?? "unknown");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to send mail notification", message = ex.Message });
        }
    }
}
