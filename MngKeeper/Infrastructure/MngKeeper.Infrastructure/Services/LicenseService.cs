using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.DTOs.License;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Infrastructure.Services
{
    /// <summary>
    /// Service for managing domain licenses
    /// </summary>
    public class LicenseService : ILicenseService
    {
        private readonly ILogger<LicenseService> _logger;
        private readonly IMinioService _minioService;
        private readonly ILicenseEncryptionService _encryptionService;
        private readonly IDomainRepository _domainRepository;
        private readonly IUserRepository _userRepository;
        private readonly IRedisService _redisService;
        private readonly MngKeeperSettings _settings;

        private const string TrialLicenseFileName = "system/license-trial.enc";
        private const string RealLicenseFileName = "system/license-real.enc";
        private const string LicenseCacheKeyPrefix = "license:";
        private const string UserCountCacheKeyPrefix = "user_count:";

        public LicenseService(
            ILogger<LicenseService> logger,
            IMinioService minioService,
            ILicenseEncryptionService encryptionService,
            IDomainRepository domainRepository,
            IUserRepository userRepository,
            IRedisService redisService,
            IOptions<MngKeeperSettings> settings)
        {
            _logger = logger;
            _minioService = minioService;
            _encryptionService = encryptionService;
            _domainRepository = domainRepository;
            _userRepository = userRepository;
            _redisService = redisService;
            _settings = settings.Value;
        }

        public async Task<LicenseInfo> CreateTrialLicenseAsync(string domainName, int days = 15, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating trial license for domain: {DomainName}, days: {Days}", domainName, days);

                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    throw new InvalidOperationException($"Domain not found: {domainName}");
                }

                var expiresAt = DateTime.UtcNow.AddDays(days);
                var licenseData = new LicenseData
                {
                    DomainName = domainName,
                    LicenseType = LicenseType.Trial,
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IssuedBy = "system",
                    LicenseKey = GenerateLicenseKey(domainName, LicenseType.Trial),
                    ExpirationBehavior = new ExpirationBehavior
                    {
                        BlockTokenGeneration = true,
                        BlockCrudOperations = true,
                        BlockGetOperations = false,
                        AllowReadOnly = true,
                        CustomMessage = null // Custom message will be shown only when license is expired
                    },
                    LicenseFeatures = new LicenseFeatures
                    {
                        MaxUsers = domain.Settings.MaxUsers,
                        CountActiveUsersOnly = true
                    }
                };

                // Generate signature (before adding signature field)
                var licenseJsonForSignature = JsonSerializer.Serialize(licenseData);
                licenseData.Signature = await _encryptionService.GenerateSignatureAsync(domainName, licenseJsonForSignature, cancellationToken);
                var licenseJson = JsonSerializer.Serialize(licenseData);

                // Encrypt and save to MinIO
                var encryptedData = await _encryptionService.EncryptLicenseAsync(domainName, licenseJson, cancellationToken);
                var stream = new MemoryStream(encryptedData);
                var saved = await _minioService.PutObjectAsync(
                    domain.StorageBucket,
                    TrialLicenseFileName,
                    stream,
                    "application/octet-stream",
                    cancellationToken);

                if (!saved)
                {
                    throw new InvalidOperationException("Failed to save trial license to MinIO");
                }

                // Update domain entity
                domain.LicenseInfo.TrialLicenseExpiresAt = expiresAt;
                domain.LicenseInfo.ActiveLicenseType = LicenseType.Trial;
                domain.LicenseInfo.LastLicenseCheck = DateTime.UtcNow;
                await _domainRepository.UpdateAsync(domain);

                _logger.LogInformation("Trial license created successfully for domain: {DomainName}", domainName);
                return domain.LicenseInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create trial license for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<LicenseInfo> CreateRealLicenseAsync(
            string domainName,
            DateTime expiresAt,
            ExpirationBehavior expirationBehavior,
            LicenseFeatures licenseFeatures,
            CustomerInfo? customerInfo = null,
            LicenseMetadata? metadata = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Creating real license for domain: {DomainName}, expires at: {ExpiresAt}", domainName, expiresAt);

                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    throw new InvalidOperationException($"Domain not found: {domainName}");
                }

                var licenseData = new LicenseData
                {
                    DomainName = domainName,
                    LicenseType = LicenseType.Real,
                    IssuedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IssuedBy = "system",
                    LicenseKey = GenerateLicenseKey(domainName, LicenseType.Real),
                    ExpirationBehavior = expirationBehavior,
                    LicenseFeatures = licenseFeatures,
                    CustomerInfo = customerInfo,
                    Metadata = metadata
                };

                // Generate signature (before adding signature field)
                var licenseJsonForSignature = JsonSerializer.Serialize(licenseData);
                licenseData.Signature = await _encryptionService.GenerateSignatureAsync(domainName, licenseJsonForSignature, cancellationToken);
                var licenseJson = JsonSerializer.Serialize(licenseData);

                // Encrypt and save to MinIO
                var encryptedData = await _encryptionService.EncryptLicenseAsync(domainName, licenseJson, cancellationToken);
                var stream = new MemoryStream(encryptedData);
                var saved = await _minioService.PutObjectAsync(
                    domain.StorageBucket,
                    RealLicenseFileName,
                    stream,
                    "application/octet-stream",
                    cancellationToken);

                if (!saved)
                {
                    throw new InvalidOperationException("Failed to save real license to MinIO");
                }

                // Update domain entity
                domain.LicenseInfo.HasRealLicense = true;
                domain.LicenseInfo.RealLicenseExpiresAt = expiresAt;
                domain.LicenseInfo.ActiveLicenseType = LicenseType.Real;
                domain.LicenseInfo.LastLicenseCheck = DateTime.UtcNow;
                await _domainRepository.UpdateAsync(domain);

                // Clear license cache
                var cacheKey = $"{LicenseCacheKeyPrefix}{domainName}";
                await _redisService.DeleteAsync(cacheKey);

                // Invalidate user count cache since license features (ActiveUserDefinition) may have changed
                await InvalidateUserCountCacheAsync(domainName, cancellationToken);

                _logger.LogInformation("Real license created successfully for domain: {DomainName}", domainName);
                return domain.LicenseInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create real license for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<LicenseValidationResult> ValidateLicenseAsync(string domainName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Validating license for domain: {DomainName}", domainName);

                // Try to get from cache first
                var cacheKey = $"{LicenseCacheKeyPrefix}{domainName}";
                var cachedResult = await _redisService.GetAsync<LicenseValidationResult>(cacheKey);
                if (cachedResult != null)
                {
                    _logger.LogInformation(">>> [ValidateLicense] Cache found for domain: {DomainName}, cached LicenseType: {LicenseType}", 
                        domainName, cachedResult.LicenseType);
                    // Check if cached result is still valid by getting active license
                    // If active license type changed (e.g., Real license was created), invalidate cache
                    var currentActiveLicense = await GetActiveLicenseAsync(domainName, cancellationToken);
                    if (currentActiveLicense != null)
                    {
                        _logger.LogInformation(">>> [ValidateLicense] Active license type: {ActiveType}, cached type: {CachedType}", 
                            currentActiveLicense.LicenseType, cachedResult.LicenseType);
                        
                        if (cachedResult.LicenseType != currentActiveLicense.LicenseType)
                        {
                            _logger.LogInformation(">>> [ValidateLicense] License type changed for domain: {DomainName}, cached: {CachedType}, active: {ActiveType}. Invalidating cache.", 
                                domainName, cachedResult.LicenseType, currentActiveLicense.LicenseType);
                            await _redisService.DeleteAsync(cacheKey);
                            // Continue to re-validate below
                        }
                        else
                        {
                            _logger.LogInformation(">>> [ValidateLicense] Cache is still valid, returning cached result for domain: {DomainName}", domainName);
                            // Cache is still valid
                            return cachedResult;
                        }
                    }
                    else
                    {
                        _logger.LogWarning(">>> [ValidateLicense] Active license is null, invalidating cache for domain: {DomainName}", domainName);
                        await _redisService.DeleteAsync(cacheKey);
                        // Continue to re-validate below
                    }
                }

                var activeLicense = await GetActiveLicenseAsync(domainName, cancellationToken);
                if (activeLicense == null)
                {
                    return new LicenseValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "No license found for domain"
                    };
                }

                var isExpired = activeLicense.ExpiresAt < DateTime.UtcNow;
                var isValid = !isExpired;

                // Update domain entity
                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain != null)
                {
                    domain.LicenseInfo.LastLicenseCheck = DateTime.UtcNow;
                    domain.LicenseInfo.ActiveLicenseType = activeLicense.LicenseType;
                    if (activeLicense.LicenseType == LicenseType.Real)
                    {
                        domain.LicenseInfo.HasRealLicense = true;
                        domain.LicenseInfo.RealLicenseExpiresAt = activeLicense.ExpiresAt;
                    }
                    else
                    {
                        domain.LicenseInfo.TrialLicenseExpiresAt = activeLicense.ExpiresAt;
                    }
                    await _domainRepository.UpdateAsync(domain);
                }

                var result = new LicenseValidationResult
                {
                    IsValid = isValid,
                    IsExpired = isExpired,
                    LicenseType = activeLicense.LicenseType,
                    ExpiresAt = activeLicense.ExpiresAt,
                    ExpirationBehavior = activeLicense.ExpirationBehavior,
                    LicenseFeatures = activeLicense.LicenseFeatures
                };

                // Cache the result (1 hour TTL or until expiry, whichever is shorter)
                var ttl = isExpired 
                    ? TimeSpan.FromHours(1) 
                    : TimeSpan.FromMinutes(Math.Min(60, (activeLicense.ExpiresAt - DateTime.UtcNow).TotalMinutes));
                await _redisService.SetAsync(cacheKey, result, ttl);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate license for domain: {DomainName}", domainName);
                return new LicenseValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"License validation failed: {ex.Message}"
                };
            }
        }

        public async Task<LicenseData?> GetLicenseAsync(string domainName, LicenseType type, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting license for domain: {DomainName}, type: {Type}", domainName, type);

                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    _logger.LogWarning("Domain not found: {DomainName}", domainName);
                    return null;
                }

                var fileName = type == LicenseType.Trial ? TrialLicenseFileName : RealLicenseFileName;
                var stream = await _minioService.GetObjectAsync(domain.StorageBucket, fileName, cancellationToken);
                if (stream == null)
                {
                    _logger.LogDebug("License file not found: {DomainName}, type: {Type}", domainName, type);
                    return null;
                }

                // Read encrypted data
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                var encryptedData = memoryStream.ToArray();

                // Decrypt
                var licenseJson = await _encryptionService.DecryptLicenseAsync(domainName, encryptedData, cancellationToken);
                var licenseData = JsonSerializer.Deserialize<LicenseData>(licenseJson);

                // Validate signature (without signature field)
                if (licenseData != null)
                {
                    // Create a copy without signature for validation
                    var licenseDataForValidation = new LicenseData
                    {
                        DomainName = licenseData.DomainName,
                        LicenseType = licenseData.LicenseType,
                        IssuedAt = licenseData.IssuedAt,
                        ExpiresAt = licenseData.ExpiresAt,
                        IssuedBy = licenseData.IssuedBy,
                        LicenseKey = licenseData.LicenseKey,
                        Signature = string.Empty, // Empty for validation
                        ExpirationBehavior = licenseData.ExpirationBehavior,
                        CustomerInfo = licenseData.CustomerInfo,
                        LicenseFeatures = licenseData.LicenseFeatures,
                        Metadata = licenseData.Metadata
                    };
                    var licenseJsonForValidation = JsonSerializer.Serialize(licenseDataForValidation);
                    var signatureValid = await _encryptionService.ValidateSignatureAsync(
                        domainName,
                        licenseJsonForValidation,
                        licenseData.Signature,
                        cancellationToken);

                    if (!signatureValid)
                    {
                        _logger.LogWarning("License signature validation failed: {DomainName}, type: {Type}", domainName, type);
                        return null;
                    }
                }

                return licenseData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get license for domain: {DomainName}, type: {Type}", domainName, type);
                return null;
            }
        }

        public async Task<LicenseData?> GetActiveLicenseAsync(string domainName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(">>> [GetActiveLicense] Getting active license for domain: {DomainName}", domainName);
                
                // Priority: Real license always takes precedence if it exists (even if expired)
                // Only fallback to Trial if Real license doesn't exist
                var realLicense = await GetLicenseAsync(domainName, LicenseType.Real, cancellationToken);
                if (realLicense != null)
                {
                    var isExpired = realLicense.ExpiresAt < DateTime.UtcNow;
                    _logger.LogInformation(">>> [GetActiveLicense] Real license found for domain: {DomainName}, expiresAt: {ExpiresAt}, now: {Now}, isExpired: {IsExpired}, licenseType: {LicenseType}", 
                        domainName, realLicense.ExpiresAt, DateTime.UtcNow, isExpired, realLicense.LicenseType);
                    
                    // Real license exists - use it regardless of expiration status
                    // Expiration behavior will be handled by the validation logic
                    _logger.LogInformation(">>> [GetActiveLicense] Using Real license for domain: {DomainName} (expired: {IsExpired})", 
                        domainName, isExpired);
                    return realLicense;
                }
                else
                {
                    _logger.LogInformation(">>> [GetActiveLicense] No Real license found for domain: {DomainName}, falling back to Trial", domainName);
                }

                // Fallback to Trial license only if Real license doesn't exist
                var trialLicense = await GetLicenseAsync(domainName, LicenseType.Trial, cancellationToken);
                if (trialLicense != null)
                {
                    var isExpired = trialLicense.ExpiresAt < DateTime.UtcNow;
                    _logger.LogInformation(">>> [GetActiveLicense] Trial license found for domain: {DomainName}, expiresAt: {ExpiresAt}, now: {Now}, isExpired: {IsExpired}, licenseType: {LicenseType}", 
                        domainName, trialLicense.ExpiresAt, DateTime.UtcNow, isExpired, trialLicense.LicenseType);
                    
                    // Return Trial license (expired or not) - expiration behavior will be handled by validation
                    _logger.LogInformation(">>> [GetActiveLicense] Using Trial license for domain: {DomainName} (expired: {IsExpired})", 
                        domainName, isExpired);
                    return trialLicense;
                }
                else
                {
                    _logger.LogWarning(">>> [GetActiveLicense] No license found for domain: {DomainName}", domainName);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [GetActiveLicense] Failed to get active license for domain: {DomainName}", domainName);
                return null;
            }
        }

        public async Task<bool> UploadRealLicenseAsync(string domainName, Stream licenseFile, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Uploading real license for domain: {DomainName}", domainName);

                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    throw new InvalidOperationException($"Domain not found: {domainName}");
                }

                // Read license file
                using var memoryStream = new MemoryStream();
                await licenseFile.CopyToAsync(memoryStream, cancellationToken);
                var encryptedData = memoryStream.ToArray();

                // Decrypt to validate
                var licenseJson = await _encryptionService.DecryptLicenseAsync(domainName, encryptedData, cancellationToken);
                var licenseData = JsonSerializer.Deserialize<LicenseData>(licenseJson);

                if (licenseData == null || licenseData.LicenseType != LicenseType.Real)
                {
                    throw new InvalidOperationException("Invalid real license format");
                }

                // Validate signature (without signature field)
                var licenseDataForValidation = new LicenseData
                {
                    DomainName = licenseData.DomainName,
                    LicenseType = licenseData.LicenseType,
                    IssuedAt = licenseData.IssuedAt,
                    ExpiresAt = licenseData.ExpiresAt,
                    IssuedBy = licenseData.IssuedBy,
                    LicenseKey = licenseData.LicenseKey,
                    Signature = string.Empty, // Empty for validation
                    ExpirationBehavior = licenseData.ExpirationBehavior,
                    CustomerInfo = licenseData.CustomerInfo,
                    LicenseFeatures = licenseData.LicenseFeatures,
                    Metadata = licenseData.Metadata
                };
                var licenseJsonForValidation = JsonSerializer.Serialize(licenseDataForValidation);
                var signatureValid = await _encryptionService.ValidateSignatureAsync(
                    domainName,
                    licenseJsonForValidation,
                    licenseData.Signature,
                    cancellationToken);

                if (!signatureValid)
                {
                    throw new InvalidOperationException("License signature validation failed");
                }

                // Save to MinIO
                var stream = new MemoryStream(encryptedData);
                var saved = await _minioService.PutObjectAsync(
                    domain.StorageBucket,
                    RealLicenseFileName,
                    stream,
                    "application/octet-stream",
                    cancellationToken);

                if (!saved)
                {
                    throw new InvalidOperationException("Failed to save real license to MinIO");
                }

                // Update domain entity
                domain.LicenseInfo.HasRealLicense = true;
                domain.LicenseInfo.RealLicenseExpiresAt = licenseData.ExpiresAt;
                domain.LicenseInfo.ActiveLicenseType = LicenseType.Real;
                domain.LicenseInfo.LastLicenseCheck = DateTime.UtcNow;
                await _domainRepository.UpdateAsync(domain);

                // Clear license cache
                var cacheKey = $"{LicenseCacheKeyPrefix}{domainName}";
                await _redisService.DeleteAsync(cacheKey);

                // Invalidate user count cache since license features (ActiveUserDefinition) may have changed
                await InvalidateUserCountCacheAsync(domainName, cancellationToken);

                _logger.LogInformation("Real license uploaded successfully for domain: {DomainName}", domainName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload real license for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<bool> RenewLicenseAsync(string domainName, DateTime newExpiryDate, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Renewing license for domain: {DomainName}, new expiry: {ExpiryDate}", domainName, newExpiryDate);

                var activeLicense = await GetActiveLicenseAsync(domainName, cancellationToken);
                if (activeLicense == null)
                {
                    throw new InvalidOperationException("No active license found to renew");
                }

                // Update expiry date
                activeLicense.ExpiresAt = newExpiryDate;

                // Re-encrypt and save (generate new signature)
                var licenseDataForSignature = new LicenseData
                {
                    DomainName = activeLicense.DomainName,
                    LicenseType = activeLicense.LicenseType,
                    IssuedAt = activeLicense.IssuedAt,
                    ExpiresAt = activeLicense.ExpiresAt,
                    IssuedBy = activeLicense.IssuedBy,
                    LicenseKey = activeLicense.LicenseKey,
                    Signature = string.Empty, // Empty for signature generation
                    ExpirationBehavior = activeLicense.ExpirationBehavior,
                    CustomerInfo = activeLicense.CustomerInfo,
                    LicenseFeatures = activeLicense.LicenseFeatures,
                    Metadata = activeLicense.Metadata
                };
                var licenseJsonForSignature = JsonSerializer.Serialize(licenseDataForSignature);
                activeLicense.Signature = await _encryptionService.GenerateSignatureAsync(domainName, licenseJsonForSignature, cancellationToken);
                var licenseJson = JsonSerializer.Serialize(activeLicense);

                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    throw new InvalidOperationException($"Domain not found: {domainName}");
                }

                var encryptedData = await _encryptionService.EncryptLicenseAsync(domainName, licenseJson, cancellationToken);
                var stream = new MemoryStream(encryptedData);
                var fileName = activeLicense.LicenseType == LicenseType.Real ? RealLicenseFileName : TrialLicenseFileName;
                var saved = await _minioService.PutObjectAsync(
                    domain.StorageBucket,
                    fileName,
                    stream,
                    "application/octet-stream",
                    cancellationToken);

                if (!saved)
                {
                    throw new InvalidOperationException("Failed to save renewed license to MinIO");
                }

                // Update domain entity
                if (activeLicense.LicenseType == LicenseType.Real)
                {
                    domain.LicenseInfo.RealLicenseExpiresAt = newExpiryDate;
                }
                else
                {
                    domain.LicenseInfo.TrialLicenseExpiresAt = newExpiryDate;
                }
                domain.LicenseInfo.LastLicenseCheck = DateTime.UtcNow;
                await _domainRepository.UpdateAsync(domain);

                // Clear license cache
                var cacheKey = $"{LicenseCacheKeyPrefix}{domainName}";
                await _redisService.DeleteAsync(cacheKey);

                _logger.LogInformation("License renewed successfully for domain: {DomainName}", domainName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to renew license for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<bool> IsOperationAllowedAsync(string domainName, LicenseOperation operation, CancellationToken cancellationToken = default)
        {
            try
            {
                var validation = await ValidateLicenseAsync(domainName, cancellationToken);
                
                // If no license found or no expiration behavior configured, deny all operations
                if (validation.ExpirationBehavior == null)
                {
                    _logger.LogWarning("No expiration behavior configured for domain: {DomainName}, denying operation: {Operation}", 
                        domainName, operation);
                    return false;
                }

                // If license is valid and not expired, allow all operations
                if (validation.IsValid && !validation.IsExpired)
                {
                    _logger.LogDebug("License is valid for domain: {DomainName}, allowing operation: {Operation}", 
                        domainName, operation);
                    return true;
                }

                // If license is expired, check expiration behavior settings
                if (validation.IsExpired)
                {
                    var isAllowed = operation switch
                    {
                        LicenseOperation.TokenGeneration => !validation.ExpirationBehavior.BlockTokenGeneration,
                        LicenseOperation.CrudOperation => !validation.ExpirationBehavior.BlockCrudOperations,
                        LicenseOperation.GetOperation => !validation.ExpirationBehavior.BlockGetOperations,
                        _ => false
                    };

                    _logger.LogInformation(
                        "License expired for domain: {DomainName}, operation: {Operation}, blockTokenGeneration: {BlockToken}, blockCrud: {BlockCrud}, blockGet: {BlockGet}, isAllowed: {IsAllowed}",
                        domainName, operation,
                        validation.ExpirationBehavior.BlockTokenGeneration,
                        validation.ExpirationBehavior.BlockCrudOperations,
                        validation.ExpirationBehavior.BlockGetOperations,
                        isAllowed);

                    return isAllowed;
                }

                // License exists but validation failed for other reasons, deny operations
                _logger.LogWarning("License validation failed for domain: {DomainName}, denying operation: {Operation}", 
                    domainName, operation);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check operation permission for domain: {DomainName}", domainName);
                return false;
            }
        }

        public async Task<int> GetActiveUserCountAsync(string domainName, CancellationToken cancellationToken = default)
        {
            try
            {
                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    return 0;
                }

                // Try cache first
                var cacheKey = $"{UserCountCacheKeyPrefix}{domain.Id}:active";
                var cachedCount = await _redisService.GetAsync<int?>(cacheKey);
                if (cachedCount.HasValue)
                {
                    _logger.LogDebug("Active user count found in cache for domain: {DomainName}, count: {Count}", domainName, cachedCount.Value);
                    return cachedCount.Value;
                }

                // Get active license to check user definition
                var activeLicense = await GetActiveLicenseAsync(domainName, cancellationToken);
                if (activeLicense?.LicenseFeatures?.ActiveUserDefinition == null)
                {
                    // Default: count all active users
                    var allUsers = await _userRepository.GetByDomainIdAsync(domain.Id);
                    var activeUserCount = allUsers.Count(u => u.IsActive);
                    
                    // Cache for 5 minutes
                    await _redisService.SetAsync(cacheKey, activeUserCount, TimeSpan.FromMinutes(5));
                    return activeUserCount;
                }

                var definition = activeLicense.LicenseFeatures.ActiveUserDefinition;
                var allDomainUsers = await _userRepository.GetByDomainIdAsync(domain.Id);
                
                // Log total users and their status
                var totalUsers = allDomainUsers.Count();
                var activeUsers = allDomainUsers.Count(u => u.IsActive);
                var inactiveUsers = allDomainUsers.Count(u => !u.IsActive);
                _logger.LogInformation("GetActiveUserCount: Domain: {DomainName}, Total: {Total}, Active: {Active}, Inactive: {Inactive}, Definition.IsActive: {DefIsActive}, LastLoginDays: {LastLoginDays}", 
                    domainName, totalUsers, activeUsers, inactiveUsers, definition.IsActive, definition.LastLoginDays);
                
                var filteredUsers = allDomainUsers.Where(u => u.IsActive == definition.IsActive);
                var afterIsActiveFilter = filteredUsers.Count();
                _logger.LogInformation("GetActiveUserCount: After IsActive filter (== {DefIsActive}), Count: {Count}", definition.IsActive, afterIsActiveFilter);

                if (definition.LastLoginDays.HasValue)
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-definition.LastLoginDays.Value);
                    var usersWithLogin = filteredUsers.Count(u => u.LastLoginAt.HasValue);
                    var usersWithoutLogin = filteredUsers.Count(u => !u.LastLoginAt.HasValue);
                    _logger.LogInformation("GetActiveUserCount: Before LastLoginDays filter, WithLogin: {WithLogin}, WithoutLogin: {WithoutLogin}, CutoffDate: {CutoffDate}", 
                        usersWithLogin, usersWithoutLogin, cutoffDate);
                    
                    // Include users with no login (null LastLoginAt) OR users who logged in within the period
                    // This way, newly created active users are counted even if they haven't logged in yet
                    filteredUsers = filteredUsers.Where(u => !u.LastLoginAt.HasValue || u.LastLoginAt >= cutoffDate);
                    var afterLoginFilter = filteredUsers.Count();
                    _logger.LogInformation("GetActiveUserCount: After LastLoginDays filter (including null LastLoginAt), Count: {Count}", afterLoginFilter);
                }

                var filteredUserCount = filteredUsers.Count();
                
                // Cache for 5 minutes
                await _redisService.SetAsync(cacheKey, filteredUserCount, TimeSpan.FromMinutes(5));
                
                return filteredUserCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active user count for domain: {DomainName}", domainName);
                return 0;
            }
        }

        public async Task<bool> CanCreateUserAsync(string domainName, CancellationToken cancellationToken = default)
        {
            try
            {
                var activeLicense = await GetActiveLicenseAsync(domainName, cancellationToken);
                if (activeLicense?.LicenseFeatures == null)
                {
                    return true; // No license features means no limit
                }

                var currentCount = await GetActiveUserCountAsync(domainName, cancellationToken);
                return currentCount < activeLicense.LicenseFeatures.MaxUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if user can be created for domain: {DomainName}", domainName);
                return false;
            }
        }

        public async Task InvalidateUserCountCacheAsync(string domainName, CancellationToken cancellationToken = default)
        {
            try
            {
                var domain = await _domainRepository.GetByNameAsync(domainName);
                if (domain == null)
                {
                    _logger.LogWarning("Cannot invalidate user count cache: Domain not found: {DomainName}", domainName);
                    return;
                }

                var cacheKey = $"{UserCountCacheKeyPrefix}{domain.Id}:active";
                await _redisService.DeleteAsync(cacheKey);
                _logger.LogInformation("User count cache invalidated for domain: {DomainName}", domainName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invalidate user count cache for domain: {DomainName}", domainName);
            }
        }

        private string GenerateLicenseKey(string domainName, LicenseType type)
        {
            var input = $"{type}-{domainName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(hash);
        }
    }
}
