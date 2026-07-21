using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngKeeper.Application.Common;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.DTOs;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers;

/// <summary>
/// Internal service endpoints (Notifier Telegram bind). No JWT — API key when configured.
/// </summary>
[ApiController]
[Route("api/internal")]
[AllowAnonymous]
public class InternalTelegramController : ControllerBase
{
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private readonly IUserRepository _userRepository;
    private readonly IDataGatewaySyncService _dataGatewaySyncService;
    private readonly MngKeeperSettings _settings;
    private readonly ILogger<InternalTelegramController> _logger;

    public InternalTelegramController(
        IUserRepository userRepository,
        IDataGatewaySyncService dataGatewaySyncService,
        IOptions<MngKeeperSettings> settings,
        ILogger<InternalTelegramController> logger)
    {
        _userRepository = userRepository;
        _dataGatewaySyncService = dataGatewaySyncService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Bind Telegram chat_id to a user (called by MngNotifier after /start link_…).
    /// </summary>
    [HttpPost("telegram-link")]
    [ProducesResponseType(typeof(TelegramLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelegramLinkResponse>> TelegramLink(
        [FromBody] TelegramLinkRequest? request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.InternalNotifyApiKey))
        {
            if (!Request.Headers.TryGetValue(NotifyApiKeyHeaderName, out var supplied) ||
                supplied.Count != 1 ||
                !string.Equals(supplied.ToString(), _settings.InternalNotifyApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(new TelegramLinkResponse { Linked = false, Error = "Invalid or missing notify API key" });
            }
        }

        if (request == null)
            return BadRequest(new TelegramLinkResponse { Linked = false, Error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(request.DomainId))
            return BadRequest(new TelegramLinkResponse { Linked = false, Error = "DomainId is required" });

        if (string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new TelegramLinkResponse { Linked = false, Error = "UserId is required" });

        if (string.IsNullOrWhiteSpace(request.TelegramChatId))
            return BadRequest(new TelegramLinkResponse { Linked = false, Error = "TelegramChatId is required" });

        var user = await _userRepository.GetByIdAsync(request.UserId.Trim(), request.DomainId.Trim());
        if (user == null)
        {
            return NotFound(new TelegramLinkResponse
            {
                Linked = false,
                UserId = request.UserId,
                Error = "User not found"
            });
        }

        TelegramUserProfileHelper.ApplyFromRequest(user, request.TelegramUsername, request.TelegramChatId);
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;

        var updated = await _userRepository.UpdateAsync(user);

        try
        {
            await _dataGatewaySyncService.SyncUserToDataGatewayAsync(updated, request.DomainId.Trim(), null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DataGateway sync after telegram-link failed for {UserId}", updated.Id);
        }

        _logger.LogInformation(
            "Telegram linked: UserId={UserId} DomainId={DomainId} ChatId={ChatId} Username={Username}",
            updated.Id, request.DomainId, updated.TelegramChatId, updated.TelegramUsername);

        return Ok(new TelegramLinkResponse
        {
            Linked = true,
            UserId = updated.Id,
            TelegramChatId = updated.TelegramChatId,
            TelegramUsername = updated.TelegramUsername,
            TelegramLinkedAt = updated.TelegramLinkedAt
        });
    }

    /// <summary>
    /// Resolve telegramChatId for user ids (Document / Reporting notify).
    /// </summary>
    [HttpPost("telegram-resolve-recipients")]
    [ProducesResponseType(typeof(TelegramResolveRecipientsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TelegramResolveRecipientsResponse>> TelegramResolveRecipients(
        [FromBody] TelegramResolveRecipientsRequest? request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_settings.InternalNotifyApiKey))
        {
            if (!Request.Headers.TryGetValue(NotifyApiKeyHeaderName, out var supplied) ||
                supplied.Count != 1 ||
                !string.Equals(supplied.ToString(), _settings.InternalNotifyApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(new TelegramResolveRecipientsResponse { Error = "Invalid or missing notify API key" });
            }
        }

        if (request == null)
            return BadRequest(new TelegramResolveRecipientsResponse { Error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(request.DomainId))
            return BadRequest(new TelegramResolveRecipientsResponse { Error = "DomainId is required" });

        var userIds = (request.UserIds ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (userIds.Count == 0)
            return BadRequest(new TelegramResolveRecipientsResponse { Error = "At least one UserId is required" });

        var response = new TelegramResolveRecipientsResponse();
        var chatSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var userId in userIds)
        {
            var user = await _userRepository.GetByIdAsync(userId, request.DomainId.Trim());
            var item = new TelegramResolveRecipientItem { UserId = userId };
            if (user != null && !string.IsNullOrWhiteSpace(user.TelegramChatId))
            {
                item.TelegramChatId = user.TelegramChatId;
                item.TelegramUsername = user.TelegramUsername;
                item.HasChatId = true;
                if (chatSet.Add(user.TelegramChatId))
                    response.ChatIds.Add(user.TelegramChatId);
            }

            response.Results.Add(item);
        }

        return Ok(response);
    }
}
