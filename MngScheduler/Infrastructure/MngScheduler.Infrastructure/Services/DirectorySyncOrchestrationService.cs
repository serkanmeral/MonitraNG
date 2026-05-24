using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Infrastructure.Services;

public class DirectorySyncOrchestrationService : IDirectorySyncOrchestrationService
{
    private readonly IDomainLookupService _domainLookupService;
    private readonly IMngKeeperDirectorySyncClient _keeperClient;
    private readonly ILogger<DirectorySyncOrchestrationService> _logger;
    private readonly DirectorySyncOrchestrationSettings _settings;

    public DirectorySyncOrchestrationService(
        IDomainLookupService domainLookupService,
        IMngKeeperDirectorySyncClient keeperClient,
        ILogger<DirectorySyncOrchestrationService> logger,
        IOptions<MngSchedulerSettings> settings)
    {
        _domainLookupService = domainLookupService;
        _keeperClient = keeperClient;
        _logger = logger;
        _settings = settings.Value.DirectorySyncOrchestration;
    }

    public async Task<DirectorySyncOrchestrationResult> RunAsync(
        Dictionary<string, string>? requestHeaders = null,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("[DirectorySync] Orchestration disabled in configuration");
            return new DirectorySyncOrchestrationResult
            {
                IsSuccess = true,
                Summary = "Orchestration disabled in configuration."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new DirectorySyncOrchestrationResult();

        var domains = (await _domainLookupService.GetActiveDomainsAsync()).ToList();
        result.DomainsTotal = domains.Count;

        _logger.LogInformation(
            "[DirectorySync] Orchestration started; activeDomains={Count} names={DomainNames}",
            domains.Count,
            domains.Count > 0
                ? string.Join(", ", domains.Select(d => d.Name))
                : "(none)");

        if (domains.Count == 0)
        {
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccess = true;
            result.Summary = "No active domains.";
            _logger.LogWarning(
                "[DirectorySync] No active domains in mngkeeper.domains (status=Active). Keeper sync will not run.");
            return result;
        }

        foreach (var domain in domains.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keeperKey = _settings.UseDomainNameAsRealm && !string.IsNullOrWhiteSpace(domain.Name)
                ? domain.Name
                : domain.Id;

            var attempt = new DirectorySyncDomainAttempt
            {
                DomainId = domain.Id,
                DomainName = domain.Name,
                KeeperDomainKey = keeperKey
            };

            try
            {
                var response = await _keeperClient.TriggerScheduledSyncAsync(
                    keeperKey, requestHeaders, cancellationToken);

                attempt.HttpStatusCode = response.StatusCode;
                attempt.Code = response.Code;
                attempt.Message = response.Message;

                if (response.IsSkipped)
                {
                    attempt.Outcome = "skipped";
                    result.DomainsSkipped++;
                    _logger.LogInformation(
                        "[DirectorySync] Skipped (409) domain={DomainName} domainId={DomainId}",
                        domain.Name, domain.Id);
                }
                else if (response.IsSuccess)
                {
                    attempt.Outcome = "success";
                    result.DomainsSucceeded++;
                    _logger.LogInformation(
                        "[DirectorySync] Completed domain={DomainName} domainId={DomainId} HTTP {Status}",
                        domain.Name, domain.Id, response.StatusCode);
                }
                else
                {
                    attempt.Outcome = "failed";
                    result.DomainsFailed++;
                    _logger.LogError(
                        "[DirectorySync] Failed domain={DomainName} domainId={DomainId} HTTP {Status} message={Message}",
                        domain.Name, domain.Id, response.StatusCode, response.Message);

                    if (!_settings.ContinueOnDomainError)
                        break;
                }
            }
            catch (Exception ex)
            {
                attempt.Outcome = "failed";
                attempt.Message = ex.Message;
                result.DomainsFailed++;
                _logger.LogError(ex,
                    "[DirectorySync] Request failed domain={DomainName} domainId={DomainId}",
                    domain.Name, domain.Id);

                if (!_settings.ContinueOnDomainError)
                    break;
            }

            result.Domains.Add(attempt);
        }

        stopwatch.Stop();
        result.DurationMs = stopwatch.ElapsedMilliseconds;
        result.IsSuccess = result.DomainsFailed == 0 || (result.DomainsSucceeded + result.DomainsSkipped > 0);
        result.Summary =
            $"total={result.DomainsTotal}, ok={result.DomainsSucceeded}, skipped={result.DomainsSkipped}, failed={result.DomainsFailed}, ms={result.DurationMs}";

        _logger.LogInformation("[DirectorySync] Orchestration finished: {Summary}", result.Summary);
        return result;
    }
}
