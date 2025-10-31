using Microsoft.AspNetCore.Mvc;
using MediatR;
using MngKeeper.Application.Features.Domain.Commands.CreateDomain;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Api.Controllers
{
    /// <summary>
    /// Domain management controller for creating, reading, updating, and deleting domains
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ApiExplorerSettings(GroupName = "Domain Management")]
    // [AdminAuthorization] // Temporarily disabled for testing
    public class DomainController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IDomainRepository _domainRepository;
        private readonly ILogger<DomainController> _logger;

        public DomainController(
            IMediator mediator,
            IDomainRepository domainRepository,
            ILogger<DomainController> logger)
        {
            _mediator = mediator;
            _domainRepository = domainRepository;
            _logger = logger;
        }

        /// <summary>
        /// Creates a new domain
        /// </summary>
        /// <param name="command">The domain creation command</param>
        /// <returns>Created domain information</returns>
        /// <response code="201">Domain created successfully</response>
        /// <response code="400">If the domain data is invalid</response>
        /// <response code="409">If a domain with the same name already exists</response>
        [HttpPost]
        [ProducesResponseType(typeof(CreateDomainResponse), 201)]
        [ProducesResponseType(typeof(CreateDomainResponse), 400)]
        [ProducesResponseType(409)]
        public async Task<ActionResult<CreateDomainResponse>> CreateDomain([FromBody] CreateDomainCommand command)
        {
            try
            {
                var response = await _mediator.Send(command);
                
                if (!response.IsSuccess)
                {
                    return BadRequest(response);
                }

                return CreatedAtAction(nameof(GetDomain), new { id = response.DomainId }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating domain");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Gets a domain by its ID
        /// </summary>
        /// <param name="id">The domain ID</param>
        /// <returns>Domain information</returns>
        /// <response code="200">Domain found and returned</response>
        /// <response code="404">If the domain is not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MngKeeper.Domain.Entities.Domain), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<MngKeeper.Domain.Entities.Domain>> GetDomain(string id)
        {
            try
            {
                var domain = await _domainRepository.GetByIdAsync(id);
                
                if (domain == null)
                {
                    return NotFound();
                }

                return Ok(domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domain with id: {Id}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MngKeeper.Domain.Entities.Domain>>> GetDomains([FromQuery] MngKeeper.Domain.Entities.DomainStatus? status = null)
        {
            try
            {
                IEnumerable<MngKeeper.Domain.Entities.Domain> domains;
                
                if (status.HasValue)
                {
                    domains = await _domainRepository.GetByStatusAsync(status.Value);
                }
                else
                {
                    domains = await _domainRepository.GetAllAsync();
                }

                return Ok(domains);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domains");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("name/{name}")]
        public async Task<ActionResult<MngKeeper.Domain.Entities.Domain>> GetDomainByName(string name)
        {
            try
            {
                var domain = await _domainRepository.GetByNameAsync(name);
                
                if (domain == null)
                {
                    return NotFound();
                }

                return Ok(domain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domain by name: {Name}", name);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MngKeeper.Domain.Entities.Domain>> UpdateDomain(string id, [FromBody] MngKeeper.Domain.Entities.Domain domain)
        {
            try
            {
                var existingDomain = await _domainRepository.GetByIdAsync(id);
                
                if (existingDomain == null)
                {
                    return NotFound();
                }

                // Update properties
                existingDomain.DisplayName = domain.DisplayName;
                existingDomain.Settings = domain.Settings;
                existingDomain.UpdatedAt = DateTime.UtcNow;
                existingDomain.UpdatedBy = "system"; // TODO: Get from current user context

                var updatedDomain = await _domainRepository.UpdateAsync(existingDomain);
                return Ok(updatedDomain);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating domain with id: {Id}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDomain(string id)
        {
            try
            {
                var domain = await _domainRepository.GetByIdAsync(id);
                
                if (domain == null)
                {
                    return NotFound();
                }

                // Soft delete - update status to Deleted
                domain.Status = MngKeeper.Domain.Entities.DomainStatus.Deleted;
                domain.UpdatedAt = DateTime.UtcNow;
                domain.UpdatedBy = "system"; // TODO: Get from current user context

                await _domainRepository.UpdateAsync(domain);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting domain with id: {Id}", id);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}
