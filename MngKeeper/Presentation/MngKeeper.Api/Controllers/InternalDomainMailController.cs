using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Api.Controllers;

/// <summary>
/// SMTP settings for MngNotifier. Includes the password. Same API key as other internal Keeper calls.
/// </summary>
[ApiController]
[Route("api/internal/domain-mail")]
[AllowAnonymous]
public class InternalDomainMailController : ControllerBase
{
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private readonly IDomainRepository _domainRepository;
    private readonly MngKeeperSettings _settings;
    private readonly ILogger<InternalDomainMailController> _logger;

    public InternalDomainMailController(
        IDomainRepository domainRepository,
        IOptions<MngKeeperSettings> settings,
        ILogger<InternalDomainMailController> logger)
    {
        _domainRepository = domainRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpGet("{name}")]
    [ProducesResponseType(typeof(InternalDomainMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InternalDomainMailResponse>> Get(string name)
    {
        if (!string.IsNullOrWhiteSpace(_settings.InternalNotifyApiKey))
        {
            if (!Request.Headers.TryGetValue(NotifyApiKeyHeaderName, out var supplied) ||
                supplied.Count != 1 ||
                !string.Equals(supplied.ToString(), _settings.InternalNotifyApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "Invalid or missing notify API key" });
            }
        }

        if (string.IsNullOrWhiteSpace(name))
            return NotFound();

        var domain = await _domainRepository.GetByNameAsync(name.Trim());
        if (domain == null)
            return NotFound();

        var mail = domain.Settings?.Mail;
        var configured = mail is { Enabled: true } && !string.IsNullOrWhiteSpace(mail.Host);
        _logger.LogDebug("Internal domain mail lookup {Domain} configured={Configured}", domain.Name, configured);

        return Ok(new InternalDomainMailResponse
        {
            Configured = configured,
            Enabled = mail?.Enabled ?? false,
            Host = mail?.Host ?? string.Empty,
            Port = mail?.Port > 0 ? mail.Port : 587,
            Username = mail?.Username ?? string.Empty,
            Password = mail?.Password ?? string.Empty,
            EnableSsl = mail?.EnableSsl ?? true,
            SecureSocketMode = string.IsNullOrWhiteSpace(mail?.SecureSocketMode) ? "Auto" : mail!.SecureSocketMode,
            FromEmail = mail?.FromEmail ?? string.Empty,
            FromName = mail?.FromName
        });
    }
}

public sealed class InternalDomainMailResponse
{
    public bool Configured { get; set; }
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string SecureSocketMode { get; set; } = "Auto";
    public string FromEmail { get; set; } = string.Empty;
    public string? FromName { get; set; }
}
