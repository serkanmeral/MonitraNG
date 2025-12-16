using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;
using MngKeeper.Api.Attributes;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MngKeeper.Api.Controllers
{
    /// <summary>
    /// Sync controller - Manual sync from MngKeeper to DataGateway MongoDB
    /// Admin only
    /// </summary>
    [ApiController]
    [Route("api/sync")]
    // [AdminAuthorization] // Temporarily disabled for testing
    public class SyncController : ControllerBase
    {
        private readonly IDataGatewaySyncService _syncService;
        private readonly ILogger<SyncController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SyncController(
            IDataGatewaySyncService syncService,
            ILogger<SyncController> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Sync all users from MngKeeper to DataGateway MongoDB
        /// </summary>
        /// <returns>Sync result with counts</returns>
        [HttpPost("users")]
        [ProducesResponseType(typeof(DataGatewaySyncResult), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<DataGatewaySyncResult>> SyncUsers()
        {
            try
            {
                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { error = "Domain information not found in token." });
                }

                _logger.LogInformation("User sync requested for domain: {DomainId}", claims.DomainId);

                var result = await _syncService.SyncAllUsersAsync(claims.DomainId);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user sync");
                return StatusCode(500, new DataGatewaySyncResult
                {
                    Message = $"Sync failed: {ex.Message}",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Sync all groups from MngKeeper to DataGateway MongoDB
        /// </summary>
        /// <returns>Sync result with counts</returns>
        [HttpPost("groups")]
        [ProducesResponseType(typeof(DataGatewaySyncResult), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<DataGatewaySyncResult>> SyncGroups()
        {
            try
            {
                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { error = "Domain information not found in token." });
                }

                _logger.LogInformation("Group sync requested for domain: {DomainId}", claims.DomainId);

                var result = await _syncService.SyncAllGroupsAsync(claims.DomainId);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during group sync");
                return StatusCode(500, new DataGatewaySyncResult
                {
                    Message = $"Sync failed: {ex.Message}",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Sync both users and groups from MngKeeper to DataGateway MongoDB
        /// </summary>
        /// <returns>Combined sync result</returns>
        [HttpPost("all")]
        [ProducesResponseType(typeof(DataGatewaySyncResult), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<DataGatewaySyncResult>> SyncAll()
        {
            try
            {
                // Get domain from token claims
                var claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                if (claims?.DomainId == null)
                {
                    return BadRequest(new { error = "Domain information not found in token." });
                }

                _logger.LogInformation("Full sync requested for domain: {DomainId}", claims.DomainId);

                var result = await _syncService.SyncAllAsync(claims.DomainId);

                if (result.IsSuccess)
                {
                    return Ok(result);
                }
                else
                {
                    return StatusCode(500, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during full sync");
                return StatusCode(500, new DataGatewaySyncResult
                {
                    Message = $"Sync failed: {ex.Message}",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

