using Microsoft.AspNetCore.Mvc;
using MediatR;
using MngKeeper.Application.Features.Auth.Commands.GetToken;
using MngKeeper.Application.Features.Auth.Commands.RefreshToken;
using System.ComponentModel.DataAnnotations;

namespace MngKeeper.Api.Controllers
{
    /// <summary>
    /// Authentication controller for managing user authentication and token generation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ApiExplorerSettings(GroupName = "Authentication")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Authenticates a user and returns a JWT token
        /// </summary>
        /// <param name="command">The authentication command containing username and password</param>
        /// <returns>JWT token response with access token and user information</returns>
        /// <response code="200">Returns the JWT token and user information</response>
        /// <response code="400">If the authentication fails or credentials are invalid</response>
        /// <response code="401">If the user is not authorized</response>
        [HttpPost("token")]
        [ProducesResponseType(typeof(GetTokenResponse), 200)]
        [ProducesResponseType(typeof(GetTokenResponse), 400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<GetTokenResponse>> GetToken([FromBody] GetTokenCommand command)
        {
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Refreshes an expired access token using a refresh token
        /// </summary>
        /// <param name="command">The refresh token command containing the refresh token</param>
        /// <returns>New access token and refresh token</returns>
        /// <response code="200">Returns the new tokens</response>
        /// <response code="400">If the refresh token is invalid or expired</response>
        [HttpPost("refresh")]
        [ProducesResponseType(typeof(RefreshTokenResponse), 200)]
        [ProducesResponseType(typeof(RefreshTokenResponse), 400)]
        public async Task<ActionResult<RefreshTokenResponse>> RefreshToken([FromBody] RefreshTokenCommand command)
        {
            var response = await _mediator.Send(command);
            
            if (!response.IsSuccess)
                return BadRequest(response);

            return Ok(response);
        }

        /// <summary>
        /// Logs out a user by revoking their refresh token
        /// </summary>
        /// <param name="request">The logout request containing the refresh token</param>
        /// <returns>Success response</returns>
        /// <response code="200">Successfully logged out</response>
        /// <response code="400">If the logout fails</response>
        [HttpPost("logout")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult> Logout([FromBody] LogoutRequest request)
        {
            try
            {
                // Parse domain from refresh token or use provided domain name
                var realmName = request.DomainName?.ToLower().Replace(" ", "_") ?? "default";
                
                var keycloakService = HttpContext.RequestServices.GetRequiredService<MngKeeper.Application.Interfaces.IKeycloakService>();
                var success = await keycloakService.RevokeTokenAsync(realmName, request.RefreshToken);

                if (!success)
                    return BadRequest(new { message = "Failed to revoke token" });

                return Ok(new { message = "Successfully logged out" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Logout failed: {ex.Message}" });
            }
        }
    }

    public class LogoutRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
        public string? DomainName { get; set; }
    }
}
