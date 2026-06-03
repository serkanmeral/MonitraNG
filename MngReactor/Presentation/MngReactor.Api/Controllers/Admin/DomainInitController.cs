using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.Domain;

namespace MngReactor.Api.Controllers.Admin;

/// <summary>
/// Domain init - manuel varsayılan kayıt oluşturma (RabbitMQ event yedek).
/// </summary>
[Route("api/v1/admin/domain")]
[ApiController]
[Authorize]
public class DomainInitController : ControllerBase
{
    private readonly IDomainDefaultsService _domainDefaultsService;

    public DomainInitController(IDomainDefaultsService domainDefaultsService)
    {
        _domainDefaultsService = domainDefaultsService;
    }

    /// <summary>
    /// Belirtilen domain için mon_schedules ve mon_collection_periods varsayılan kayıtlarını oluşturur.
    /// Keeper event gecikirse veya manuel tetikleme gerekiyorsa kullanılır.
    /// </summary>
    [HttpPost("{domain}/init")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> InitDomain(string domain, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { error = "domain_required", message = "Domain adı gerekli" });

        var accessToken = Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(accessToken))
        {
            var auth = await HttpContext.AuthenticateAsync();
            accessToken = auth.Properties?.Items?.TryGetValue(".Token.access_token", out var t) == true ? t : null;
        }
        var ok = await _domainDefaultsService.CreateDefaultsAsync(domain.Trim(), accessToken, cancellationToken);
        return Ok(new { success = ok, domain = domain.Trim(), message = ok ? "Varsayılanlar oluşturuldu veya zaten mevcut" : "Varsayılan oluşturma başarısız" });
    }
}
