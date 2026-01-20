using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Infrastructure.Services
{
    /// <summary>
    /// Background service that validates licenses for all domains daily
    /// Runs once per day at 02:00 AM UTC
    /// </summary>
    public class LicenseValidationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<LicenseValidationBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromDays(1); // Run once per day
        private readonly TimeSpan _initialDelay = TimeSpan.FromHours(2); // Start 2 hours after app start (02:00 AM)

        public LicenseValidationBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<LicenseValidationBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LicenseValidationBackgroundService starting...");

            // Initial delay to start at 02:00 AM
            await Task.Delay(_initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting daily license validation check...");

                    using var scope = _serviceScopeFactory.CreateScope();
                    var domainRepository = scope.ServiceProvider.GetRequiredService<IDomainRepository>();
                    var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseService>();

                    // Get all active domains
                    var domains = await domainRepository.GetByStatusAsync(DomainStatus.Active);
                    var domainList = domains.ToList();

                    _logger.LogInformation("Found {Count} active domains to validate", domainList.Count);

                    var expiredCount = 0;
                    var validCount = 0;
                    var errorCount = 0;

                    foreach (var domain in domainList)
                    {
                        try
                        {
                            var validation = await licenseService.ValidateLicenseAsync(domain.Name, stoppingToken);
                            
                            if (validation.IsExpired)
                            {
                                expiredCount++;
                                _logger.LogWarning(
                                    "Domain {DomainName} has expired license. Type: {LicenseType}, ExpiresAt: {ExpiresAt}",
                                    domain.Name,
                                    validation.LicenseType,
                                    validation.ExpiresAt);
                                
                                // Update domain status to Expired if license is expired
                                domain.Status = DomainStatus.Expired;
                                await domainRepository.UpdateAsync(domain);
                            }
                            else
                            {
                                validCount++;
                                _logger.LogDebug(
                                    "Domain {DomainName} has valid license. Type: {LicenseType}, ExpiresAt: {ExpiresAt}",
                                    domain.Name,
                                    validation.LicenseType,
                                    validation.ExpiresAt);
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            _logger.LogError(ex, "Error validating license for domain: {DomainName}", domain.Name);
                        }
                    }

                    _logger.LogInformation(
                        "Daily license validation completed. Valid: {ValidCount}, Expired: {ExpiredCount}, Errors: {ErrorCount}",
                        validCount,
                        expiredCount,
                        errorCount);

                    // Wait until next day (24 hours)
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("LicenseValidationBackgroundService is stopping...");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in license validation background service");
                    // Wait 1 hour before retrying on error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("LicenseValidationBackgroundService stopped");
        }
    }
}
