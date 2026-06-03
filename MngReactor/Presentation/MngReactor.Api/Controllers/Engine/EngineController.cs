using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Features.Engine;
using MngReactor.Application.Features.Engine.Assets;

namespace MngReactor.Api.Controllers.Engine
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    public class EngineController : BaseContoller
    {
        private readonly IMediator _mediator;
        private readonly IEngineProcessing _engineProcessing;
        private readonly IEngineConfigSync _engineConfigSync;
        private readonly IConfigStringService _configStringService;
        private readonly IEngineStatusProcessing _engineStatusProcessing;

        public EngineController(
            IMediator mediator,
            IEngineProcessing engineProcessing,
            IEngineConfigSync engineConfigSync,
            IConfigStringService configStringService,
            IEngineStatusProcessing engineStatusProcessing)
        {
            _mediator = mediator;
            _engineProcessing = engineProcessing;
            _engineConfigSync = engineConfigSync;
            _configStringService = configStringService;
            _engineStatusProcessing = engineStatusProcessing;
        }

        /// <summary>
        /// Engine status (heartbeat + hata raporu). lastSeenAt ve lastErrors günceller.
        /// </summary>
        [HttpPost("status")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> PostStatus([FromBody] EngineStatusRequest request, CancellationToken cancellationToken)
        {
            var (domain, accessToken) = await GetDomainAndTokenAsync();
            if (string.IsNullOrEmpty(domain))
                return Unauthorized();

            if (request == null || string.IsNullOrEmpty(request.EngineId) || string.IsNullOrEmpty(request.Domain))
                return BadRequest(new { error = "invalid_request", message = "engineId and domain are required" });

            var success = await _engineStatusProcessing.ProcessStatusAsync(request, domain, accessToken, cancellationToken);
            if (!success)
                return BadRequest(new { error = "status_update_failed", message = "Engine not found or domain mismatch" });

            return Ok(new { success = true });
        }

        [HttpGet("assets")]
        public async Task<IActionResult> GetEngineAssets([FromQuery]string? id)
        {
            dynamic userInfo = await GetUserInfo();

            GetEngineAssetsQueryRequest request = new GetEngineAssetsQueryRequest {
                UserInfo = userInfo ,
                EngineId = id
            };

            var resp = await _mediator.Send(request);

            return Ok(resp);
        }

        [HttpGet("create_config_text")]
        public async Task<IActionResult> CreateConfigText([FromQuery] string? id)
        {
            dynamic userInfo = await GetUserInfo();

            GetEngineAssetsQueryRequest request = new GetEngineAssetsQueryRequest
            {
                UserInfo = userInfo,
                EngineId = id
            };

            var resp = await _engineProcessing.CreateEngineConfigText(request);

            return Ok(resp);
        }

        /// <summary>
        /// Config Sync API - Engine'e agent, asset, period, schedule bilgilerini döner.
        /// </summary>
        [HttpGet("config")]
        [ProducesResponseType(typeof(EngineConfigSyncResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConfig([FromQuery] string? engineId, CancellationToken cancellationToken)
        {
            var (domain, accessToken) = await GetDomainAndTokenAsync();
            if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(engineId))
                return Unauthorized();

            var result = await _engineConfigSync.GetConfigAsync(engineId, domain, accessToken ?? "", cancellationToken);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        /// <summary>
        /// Config String - mon_engines'ten şifrelenmiş Base64 config string üretir.
        /// </summary>
        [HttpGet("config-string")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConfigString([FromQuery] string? engineId, CancellationToken cancellationToken)
        {
            var (domain, accessToken) = await GetDomainAndTokenAsync();
            if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(engineId))
                return Unauthorized();

            var configString = await _configStringService.CreateConfigStringAsync(engineId, domain, accessToken, cancellationToken);
            if (configString == null)
                return NotFound();

            return Ok(new { configString });
        }

        private async Task<string?> GetDomainFromTokenAsync()
        {
            var domain = User.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
            if (!string.IsNullOrEmpty(domain)) return domain;
            var auth = await HttpContext.AuthenticateAsync();
            var tokenValue = auth.Properties?.Items?.FirstOrDefault(x => x.Key == ".Token.access_token").Value;
            if (string.IsNullOrEmpty(tokenValue)) return null;
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokenValue);
            return token.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
        }

        private async Task<(string? domain, string? accessToken)> GetDomainAndTokenAsync()
        {
            var domain = User.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            var tokenValue = authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..]
                : (await HttpContext.AuthenticateAsync()).Properties?.Items?.FirstOrDefault(x => x.Key == ".Token.access_token").Value;
            if (!string.IsNullOrEmpty(domain)) return (domain, tokenValue);
            if (string.IsNullOrEmpty(tokenValue)) return (null, null);
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokenValue);
            domain = token.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
            return (domain, tokenValue);
        }
    }
}
