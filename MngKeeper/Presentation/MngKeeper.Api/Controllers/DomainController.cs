using System.Text.RegularExpressions;
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

                RedactMailPassword(domain);
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

                foreach (var domain in domains)
                    RedactMailPassword(domain);

                return Ok(domains);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domains");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Public branding for a tenant login host. Returns display name and logos only.
        /// </summary>
        [HttpGet("branding/{name}")]
        [ProducesResponseType(typeof(DomainBrandingResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<DomainBrandingResponse>> GetDomainBranding(string name)
        {
            var normalized = (name ?? string.Empty).Trim().ToLowerInvariant();
            if (!BrandingNamePattern.IsMatch(normalized))
                return NotFound();

            try
            {
                var domain = await _domainRepository.GetByNameAsync(normalized);
                if (domain == null || domain.Status == DomainStatus.Deleted)
                    return NotFound();

                return Ok(new DomainBrandingResponse
                {
                    Name = domain.Name,
                    DisplayName = string.IsNullOrWhiteSpace(domain.DisplayName) ? domain.Name : domain.DisplayName,
                    Logo = domain.Logo,
                    LogoDark = domain.LogoDark,
                    LogoLight = domain.LogoLight,
                    LogoUrl = domain.LogoUrl,
                    UpdatedAt = domain.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domain branding: {Name}", normalized);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private static readonly Regex BrandingNamePattern = new(
            "^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

                RedactMailPassword(domain);
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

                if (request.DiscoveryRootLabel != null)
                {
                    existingDomain.DiscoveryRootLabel =
                        string.IsNullOrWhiteSpace(request.DiscoveryRootLabel)
                            ? null
                            : request.DiscoveryRootLabel.Trim();
                }

                // Only overwrite phone/logo when the client actually sent the property
                // (UI always sends these today; keep defensive for partial clients).
                if (request.RelatedPersonPhone != null)
                    existingDomain.RelatedPersonPhone = request.RelatedPersonPhone;
                if (request.LogoDark != null)
                {
                    existingDomain.LogoDark = BlankToNull(request.LogoDark);
                    existingDomain.Logo = existingDomain.LogoDark;
                }
                else if (request.Logo != null)
                {
                    existingDomain.Logo = BlankToNull(request.Logo);
                    if (string.IsNullOrWhiteSpace(existingDomain.LogoDark))
                        existingDomain.LogoDark = existingDomain.Logo;
                }

                if (request.LogoLight != null)
                    existingDomain.LogoLight = BlankToNull(request.LogoLight);
                if (request.LogoUrl != null)
                    existingDomain.LogoUrl = request.LogoUrl;

                MergeDomainSettings(existingDomain, request.Settings);

                existingDomain.UpdatedAt = DateTime.UtcNow;
                existingDomain.UpdatedBy = "system"; // TODO: Get from current user context

                var updatedDomain = await _domainRepository.UpdateAsync(existingDomain);
                RedactMailPassword(updatedDomain);
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
            if (incoming.MaxFileSizeBytes.HasValue)
                target.MaxFileSizeBytes = incoming.MaxFileSizeBytes.Value < 0 ? 0 : incoming.MaxFileSizeBytes.Value;
            if (incoming.EnableMqtt.HasValue)
                target.EnableMqtt = incoming.EnableMqtt.Value;

            if (incoming.DirectoryPrivileges != null)
                MergeDirectoryPrivileges(target, incoming.DirectoryPrivileges);

            if (incoming.DirectoryLdap != null)
                MergeDirectoryLdap(target, incoming.DirectoryLdap);

            if (incoming.Mail != null)
                MergeMail(target, incoming.Mail);

            if (incoming.Password != null)
                MergePassword(target, incoming.Password);
        }

        private static void RedactMailPassword(MngKeeper.Domain.Entities.Domain domain)
        {
            var mail = domain.Settings?.Mail;
            if (mail != null)
            {
                mail.PasswordConfigured = !string.IsNullOrEmpty(mail.Password);
                mail.Password = string.Empty;
            }

            RedactPasswordSettings(domain);
        }

        private static void RedactPasswordSettings(MngKeeper.Domain.Entities.Domain domain)
        {
            var password = domain.Settings?.Password;
            if (password == null)
                return;

            password.ResetBindPasswordConfigured = !string.IsNullOrEmpty(password.ResetBindPassword);
            password.ResetBindPassword = string.Empty;
        }

        private static void MergePassword(DomainSettings target, DomainPasswordSettings incoming)
        {
            target.Password ??= new DomainPasswordSettings();
            var password = target.Password;

            password.Mode = string.Equals(incoming.Mode?.Trim(), DomainPasswordModes.KeycloakAndAd, StringComparison.OrdinalIgnoreCase)
                ? DomainPasswordModes.KeycloakAndAd
                : DomainPasswordModes.Keycloak;
            password.PersonnelDn = incoming.PersonnelDn?.Trim() ?? string.Empty;
            password.ResetBindUsername = incoming.ResetBindUsername?.Trim() ?? string.Empty;
            password.ServerCertificateThumbprint = incoming.ServerCertificateThumbprint?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(incoming.ResetBindPassword))
                password.ResetBindPassword = incoming.ResetBindPassword;
        }

        private static void MergeMail(DomainSettings target, DomainMailSettings incoming)
        {
            target.Mail ??= new DomainMailSettings();
            var mail = target.Mail;

            mail.Enabled = incoming.Enabled;
            mail.Host = incoming.Host?.Trim() ?? string.Empty;
            mail.Port = incoming.Port > 0 ? incoming.Port : 587;
            mail.Username = incoming.Username?.Trim() ?? string.Empty;
            mail.EnableSsl = incoming.EnableSsl;
            mail.SecureSocketMode = string.IsNullOrWhiteSpace(incoming.SecureSocketMode)
                ? "Auto"
                : incoming.SecureSocketMode.Trim();
            mail.FromEmail = incoming.FromEmail?.Trim() ?? string.Empty;
            mail.FromName = string.IsNullOrWhiteSpace(incoming.FromName) ? null : incoming.FromName.Trim();

            if (!string.IsNullOrEmpty(incoming.Password))
                mail.Password = incoming.Password;
        }

        private static string? BlankToNull(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;

        private static void MergeDirectoryPrivileges(
            DomainSettings target,
            UpdateDirectoryPrivilegeSettingsRequest incoming)
        {
            target.DirectoryPrivileges ??= new DirectoryPrivilegeSettings();

            if (incoming.AdminGroupNames != null)
            {
                target.DirectoryPrivileges.AdminGroupNames = NormalizeGroupNames(incoming.AdminGroupNames);
                target.FlatAdminGroupNames = null;
            }
            if (incoming.ManagerGroupNames != null)
            {
                target.DirectoryPrivileges.ManagerGroupNames = NormalizeGroupNames(incoming.ManagerGroupNames);
                target.FlatManagerGroupNames = null;
            }
        }

        private static List<string> NormalizeGroupNames(IEnumerable<string> names) =>
            names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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
