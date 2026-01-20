using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.DTOs.License;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Api.Controllers
{
    /// <summary>
    /// License management API
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    // Note: Authorization is handled by JWT middleware, not [Authorize] attribute
    public class LicenseController : ControllerBase
    {
        private readonly ILogger<LicenseController> _logger;
        private readonly ILicenseService _licenseService;

        public LicenseController(
            ILogger<LicenseController> logger,
            ILicenseService licenseService)
        {
            _logger = logger;
            _licenseService = licenseService;
        }

        // ============================================
        // Specific routes (must come before generic {domainName} route)
        // ============================================

        /// <summary>
        /// Upload a real license file for a domain
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult> UploadLicense([FromForm] UploadLicenseRequest request)
        {
            try
            {
                if (request.LicenseFile == null || request.LicenseFile.Length == 0)
                {
                    return BadRequest(new { error = "License file is required" });
                }

                if (string.IsNullOrEmpty(request.DomainName))
                {
                    return BadRequest(new { error = "Domain name is required" });
                }

                _logger.LogInformation("Uploading real license for domain: {DomainName}", request.DomainName);

                var success = await _licenseService.UploadRealLicenseAsync(
                    request.DomainName,
                    request.LicenseFile.OpenReadStream());

                if (!success)
                {
                    return StatusCode(500, new { error = "Failed to upload license" });
                }

                return Ok(new { message = "License uploaded successfully", domainName = request.DomainName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading license for domain: {DomainName}", request.DomainName);
                return StatusCode(500, new { error = $"Failed to upload license: {ex.Message}" });
            }
        }

        /// <summary>
        /// Validate license for a domain
        /// </summary>
        [HttpPost("validate")]
        public async Task<ActionResult<LicenseValidationResult>> ValidateLicense([FromBody] ValidateLicenseRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.DomainName))
                {
                    return BadRequest(new { error = "Domain name is required" });
                }

                var result = await _licenseService.ValidateLicenseAsync(request.DomainName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating license for domain: {DomainName}", request.DomainName);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if an operation is allowed for a domain
        /// </summary>
        [HttpPost("check-operation")]
        public async Task<ActionResult<OperationCheckResponse>> CheckOperation([FromBody] CheckOperationRequest request)
        {
            try
            {
                _logger.LogDebug("CheckOperation request received: DomainName={DomainName}, Operation={Operation}", 
                    request?.DomainName, request?.Operation);

                if (request == null)
                {
                    _logger.LogWarning("CheckOperation request is null");
                    return BadRequest(new { error = "Request body is required" });
                }

                if (string.IsNullOrEmpty(request.DomainName))
                {
                    _logger.LogWarning("CheckOperation: Domain name is required");
                    return BadRequest(new { error = "Domain name is required" });
                }

                _logger.LogDebug("Checking operation: DomainName={DomainName}, Operation={Operation}", 
                    request.DomainName, request.Operation);

                var isAllowed = await _licenseService.IsOperationAllowedAsync(
                    request.DomainName,
                    request.Operation);

                return Ok(new OperationCheckResponse
                {
                    IsAllowed = isAllowed,
                    DomainName = request.DomainName,
                    Operation = request.Operation
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking operation for domain: {DomainName}", request.DomainName);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // ============================================
        // Domain-specific routes (more specific routes first)
        // ============================================

        /// <summary>
        /// Create a real license for a domain with specified parameters
        /// </summary>
        [HttpPost("{domainName}/create-real")]
        public async Task<ActionResult> CreateRealLicense(string domainName, [FromBody] CreateRealLicenseRequest request)
        {
            try
            {
                _logger.LogInformation("Creating real license for domain: {DomainName}", domainName);

                if (request == null)
                {
                    return BadRequest(new { error = "Request body is required" });
                }

                if (request.ExpiresAt <= DateTime.UtcNow)
                {
                    return BadRequest(new { error = "ExpiresAt must be in the future" });
                }

                if (request.ExpirationBehavior == null)
                {
                    return BadRequest(new { error = "ExpirationBehavior is required" });
                }

                if (request.LicenseFeatures == null)
                {
                    return BadRequest(new { error = "LicenseFeatures is required" });
                }

                var licenseInfo = await _licenseService.CreateRealLicenseAsync(
                    domainName,
                    request.ExpiresAt,
                    request.ExpirationBehavior,
                    request.LicenseFeatures,
                    request.CustomerInfo,
                    request.Metadata);

                return Ok(new
                {
                    message = "Real license created successfully",
                    domainName = domainName,
                    expiresAt = licenseInfo.RealLicenseExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating real license for domain: {DomainName}", domainName);
                return StatusCode(500, new { error = $"Failed to create real license: {ex.Message}" });
            }
        }

        /// <summary>
        /// Recreate trial license for a domain (updates licenseFeatures)
        /// </summary>
        [HttpPost("{domainName}/recreate-trial")]
        public async Task<ActionResult> RecreateTrialLicense(string domainName, [FromQuery] int days = 15)
        {
            try
            {
                _logger.LogInformation("Recreating trial license for domain: {DomainName}, days: {Days}", domainName, days);

                var licenseInfo = await _licenseService.CreateTrialLicenseAsync(domainName, days);

                return Ok(new { 
                    message = "Trial license recreated successfully", 
                    domainName = domainName,
                    expiresAt = licenseInfo.TrialLicenseExpiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recreating trial license for domain: {DomainName}", domainName);
                return StatusCode(500, new { error = $"Failed to recreate trial license: {ex.Message}" });
            }
        }


        /// <summary>
        /// Download license file for a domain
        /// </summary>
        [HttpGet("{domainName}/download")]
        public async Task<ActionResult> DownloadLicense(string domainName, [FromQuery] string type = "real")
        {
            try
            {
                var licenseType = type.ToLower() == "trial" ? LicenseType.Trial : LicenseType.Real;
                var license = await _licenseService.GetLicenseAsync(domainName, licenseType);

                if (license == null)
                {
                    return NotFound(new { error = $"No {type} license found for domain" });
                }

                // Re-encrypt license for download
                var encryptionService = HttpContext.RequestServices.GetRequiredService<ILicenseEncryptionService>();
                
                var licenseJson = System.Text.Json.JsonSerializer.Serialize(license);
                var encryptedData = await encryptionService.EncryptLicenseAsync(domainName, licenseJson);

                var fileName = licenseType == LicenseType.Trial 
                    ? $"license-trial-{domainName}.enc" 
                    : $"license-real-{domainName}.enc";

                return File(encryptedData, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading license for domain: {DomainName}", domainName);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get active user count for a domain
        /// </summary>
        [HttpGet("{domainName}/user-count")]
        public async Task<ActionResult<UserCountResponse>> GetUserCount(string domainName)
        {
            try
            {
                var count = await _licenseService.GetActiveUserCountAsync(domainName);
                var canCreate = await _licenseService.CanCreateUserAsync(domainName);

                var activeLicense = await _licenseService.GetActiveLicenseAsync(domainName);
                var maxUsers = activeLicense?.LicenseFeatures?.MaxUsers;

                return Ok(new UserCountResponse
                {
                    DomainName = domainName,
                    ActiveUserCount = count,
                    MaxUsers = maxUsers,
                    CanCreateUser = canCreate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user count for domain: {DomainName}", domainName);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // ============================================
        // Generic route (must come last)
        // ============================================

        /// <summary>
        /// Get license information for a domain
        /// </summary>
        [HttpGet("{domainName}")]
        public async Task<ActionResult<LicenseInfoResponse>> GetLicense(string domainName)
        {
            try
            {
                // Get active license first (this has Real > Trial priority logic)
                var activeLicense = await _licenseService.GetActiveLicenseAsync(domainName);

                if (activeLicense == null)
                {
                    return NotFound(new { error = "No license found for domain" });
                }

                // Validate license (this may use cache, but we're using activeLicense for response)
                var validation = await _licenseService.ValidateLicenseAsync(domainName);

                _logger.LogInformation("GetLicense for domain: {DomainName}, LicenseType: {LicenseType}, IsValid: {IsValid}, ExpiresAt: {ExpiresAt}", 
                    domainName, activeLicense.LicenseType, validation.IsValid, activeLicense.ExpiresAt);

                var response = new LicenseInfoResponse
                {
                    DomainName = domainName,
                    LicenseType = activeLicense.LicenseType,
                    IsValid = validation.IsValid,
                    IsExpired = validation.IsExpired,
                    ExpiresAt = activeLicense.ExpiresAt,
                    IssuedAt = activeLicense.IssuedAt,
                    IssuedBy = activeLicense.IssuedBy,
                    ExpirationBehavior = activeLicense.ExpirationBehavior,
                    LicenseFeatures = activeLicense.LicenseFeatures,
                    CustomerInfo = activeLicense.CustomerInfo,
                    Metadata = activeLicense.Metadata
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting license for domain: {DomainName}", domainName);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }

    // Request/Response DTOs
    public class UploadLicenseRequest
    {
        public string DomainName { get; set; } = string.Empty;
        public IFormFile LicenseFile { get; set; } = null!;
    }

    public class ValidateLicenseRequest
    {
        public string DomainName { get; set; } = string.Empty;
    }

    public class CheckOperationRequest
    {
        public string DomainName { get; set; } = string.Empty;
        public LicenseOperation Operation { get; set; }
    }

    public class LicenseInfoResponse
    {
        public string DomainName { get; set; } = string.Empty;
        public LicenseType LicenseType { get; set; }
        public bool IsValid { get; set; }
        public bool IsExpired { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime IssuedAt { get; set; }
        public string IssuedBy { get; set; } = string.Empty;
        public ExpirationBehavior? ExpirationBehavior { get; set; }
        public LicenseFeatures? LicenseFeatures { get; set; }
        public CustomerInfo? CustomerInfo { get; set; }
        public LicenseMetadata? Metadata { get; set; }
    }

    public class OperationCheckResponse
    {
        public bool IsAllowed { get; set; }
        public string DomainName { get; set; } = string.Empty;
        public LicenseOperation Operation { get; set; }
    }

    public class UserCountResponse
    {
        public string DomainName { get; set; } = string.Empty;
        public int ActiveUserCount { get; set; }
        public int? MaxUsers { get; set; }
        public bool CanCreateUser { get; set; }
    }

    public class CreateRealLicenseRequest
    {
        public DateTime ExpiresAt { get; set; }
        public ExpirationBehavior ExpirationBehavior { get; set; } = null!;
        public LicenseFeatures LicenseFeatures { get; set; } = null!;
        public CustomerInfo? CustomerInfo { get; set; }
        public LicenseMetadata? Metadata { get; set; }
    }
}
