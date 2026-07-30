using Microsoft.AspNetCore.Mvc;
using MediatR;
using MngKeeper.Api.Contracts;
using MngKeeper.Application.Features.Domain.Commands.CreateDomain;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MongoDB.Driver;

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
        private readonly IMongoClient _mongoClient;
        private readonly ILogger<DomainController> _logger;

        public DomainController(
            IMediator mediator,
            IDomainRepository domainRepository,
            IMongoClient mongoClient,
            ILogger<DomainController> logger)
        {
            _mediator = mediator;
            _domainRepository = domainRepository;
            _mongoClient = mongoClient;
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
        public async Task<ActionResult<MngKeeper.Domain.Entities.Domain>> UpdateDomain(
            string id,
            [FromBody] UpdateDomainRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest(new { error = "Request body is required" });

                var existingDomain = await _domainRepository.GetByIdAsync(id);

                if (existingDomain == null)
                {
                    return NotFound();
                }

                if (!string.IsNullOrWhiteSpace(request.DisplayName))
                    existingDomain.DisplayName = request.DisplayName.Trim();

                // Only overwrite phone/logo when the client actually sent the property
                // (UI always sends these today; keep defensive for partial clients).
                if (request.RelatedPersonPhone != null)
                    existingDomain.RelatedPersonPhone = request.RelatedPersonPhone;
                if (request.Logo != null)
                    existingDomain.Logo = request.Logo;
                if (request.LogoUrl != null)
                    existingDomain.LogoUrl = request.LogoUrl;

                MergeDomainSettings(existingDomain, request.Settings);

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

        /// <summary>
        /// Get collections from domain database
        /// </summary>
        /// <param name="id">The domain ID</param>
        /// <returns>List of collections with document counts</returns>
        /// <response code="200">Collections found and returned</response>
        /// <response code="404">If the domain is not found</response>
        [HttpGet("{id}/collections")]
        [ProducesResponseType(typeof(List<CollectionInfoDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<List<CollectionInfoDto>>> GetDomainCollections(string id)
        {
            try
            {
                var domain = await _domainRepository.GetByIdAsync(id);
                
                if (domain == null)
                {
                    return NotFound(new { error = "Domain not found" });
                }

                var database = _mongoClient.GetDatabase(domain.DatabaseName);
                var collectionNames = await database.ListCollectionNamesAsync();
                var collections = await collectionNames.ToListAsync();

                var result = new List<CollectionInfoDto>();
                foreach (var collectionName in collections)
                {
                    // Skip system collections
                    if (collectionName.StartsWith("system.") || collectionName == "fs.chunks" || collectionName == "fs.files")
                    {
                        continue;
                    }

                    var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                    var documentCount = await collection.CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty);
                    
                    // Check if collection has indexes
                    var indexes = await collection.Indexes.ListAsync();
                    var indexList = await indexes.ToListAsync();
                    var hasIndexes = indexList.Count > 1; // More than just the default _id index

                    result.Add(new CollectionInfoDto
                    {
                        Name = collectionName,
                        DocumentCount = (int)documentCount,
                        HasIndexes = hasIndexes
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collections for domain with id: {Id}", id);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Merges incoming settings into existing so partial UI payloads do not wipe nested config
        /// (directoryPrivileges, mqttSettings, directoryLdap password, etc.).
        /// </summary>
        private static void MergeDomainSettings(
            MngKeeper.Domain.Entities.Domain existingDomain,
            UpdateDomainSettingsRequest? incoming)
        {
            if (incoming == null)
                return;

            existingDomain.Settings ??= new DomainSettings();
            var target = existingDomain.Settings;

            if (incoming.MaxUsers.HasValue)
                target.MaxUsers = incoming.MaxUsers.Value;
            if (incoming.MaxAssets.HasValue)
                target.MaxAssets = incoming.MaxAssets.Value;
            if (incoming.EnableMqtt.HasValue)
                target.EnableMqtt = incoming.EnableMqtt.Value;

            // Only replace privileges when the client sent at least one group name.
            // Empty arrays from a partial form must never wipe Mongo configuration.
            if (HasDirectoryPrivilegePayload(incoming.DirectoryPrivileges))
                target.DirectoryPrivileges = incoming.DirectoryPrivileges!;

            if (incoming.DirectoryLdap != null)
                MergeDirectoryLdap(target, incoming.DirectoryLdap);
        }

        private static bool HasDirectoryPrivilegePayload(DirectoryPrivilegeSettings? privileges)
        {
            if (privileges == null)
                return false;

            return (privileges.AdminGroupNames?.Count ?? 0) > 0
                || (privileges.ManagerGroupNames?.Count ?? 0) > 0;
        }

        private static void MergeDirectoryLdap(DomainSettings target, DirectoryLdapSettings incoming)
        {
            target.DirectoryLdap ??= new DirectoryLdapSettings();
            var ldap = target.DirectoryLdap;

            ldap.Enabled = incoming.Enabled;
            ldap.Host = incoming.Host ?? string.Empty;
            ldap.Port = incoming.Port > 0 ? incoming.Port : 389;
            ldap.UseSsl = incoming.UseSsl;
            ldap.BaseDn = incoming.BaseDn ?? string.Empty;
            ldap.BindUsername = incoming.BindUsername ?? string.Empty;

            // Empty password on update means "keep existing" (plain storage otherwise).
            if (!string.IsNullOrEmpty(incoming.BindPassword))
                ldap.BindPassword = incoming.BindPassword;
        }
    }

    /// <summary>
    /// Collection information DTO
    /// </summary>
    public class CollectionInfoDto
    {
        public string Name { get; set; } = string.Empty;
        public int DocumentCount { get; set; }
        public bool HasIndexes { get; set; }
    }
}
