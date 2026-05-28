using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Services;

/// <summary>
/// Service implementation for User Job operations
/// </summary>
public class UserJobService : IUserJobService
{
    private readonly IUserJobRepository _repository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IJobSyncService _syncService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserJobService> _logger;

    public UserJobService(
        IUserJobRepository repository,
        IJobExecutionRepository executionRepository,
        IJobSyncService syncService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserJobService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? GetDomainIdFromToken()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return null;
            }

            // Try domain_id claim first
            var domainId = user.FindFirst("domain_id")?.Value;
            if (!string.IsNullOrEmpty(domainId))
            {
                return domainId;
            }

            // Try TokenClaims from HttpContext.Items (if JwtClaimsMiddleware is used)
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("TokenClaims", out var tokenClaimsObj) == true)
            {
                // Use reflection to get DomainId property if available
                var tokenClaimsType = tokenClaimsObj?.GetType();
                var domainIdProperty = tokenClaimsType?.GetProperty("DomainId");
                if (domainIdProperty != null)
                {
                    var domainIdValue = domainIdProperty.GetValue(tokenClaimsObj)?.ToString();
                    if (!string.IsNullOrEmpty(domainIdValue))
                    {
                        return domainIdValue;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting domain ID from token");
            return null;
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetAllJobsAsync(string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            return await _repository.GetJobsByDomainAsync(domainId, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all user jobs");
            throw;
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetActiveJobsAsync(string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            return await _repository.GetActiveJobsByDomainAsync(domainId, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active user jobs");
            throw;
        }
    }

    public async Task<ScheduledJob?> GetJobByIdAsync(string jobId, string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            var job = await _repository.GetJobByIdAsync(domainId, jobId, token);
            
            // Ensure user can only access their own domain's jobs
            if (job != null && job.DomainId != domainId)
            {
                _logger.LogWarning("User attempted to access job from different domain. JobId: {JobId}, UserDomainId: {UserDomainId}, JobDomainId: {JobDomainId}",
                    jobId, domainId, job.DomainId);
                return null;
            }

            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user job by ID: {JobId}", jobId);
            throw;
        }
    }

    public async Task<ScheduledJob> CreateJobAsync(ScheduledJob job, string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            var userId = GetUserIdFromToken();

            // Validation
            ValidateJob(job);

            // Ensure job type is User
            job.JobType = JobType.User;
            job.DomainId = domainId;
            job.CreatedBy = userId;

            // Ensure POST payload
            job.EnsurePostPayload();

            var createdJob = await _repository.CreateJobAsync(domainId, job, token);

            // Trigger immediate sync
            await _syncService.SyncNowAsync();

            _logger.LogInformation("Created user job: {JobId} in domain {DomainId}", job.JobId, domainId);
            return createdJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<ScheduledJob> UpdateJobAsync(ScheduledJob job, string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            // API istemcileri (ör. MngOperations sync) domainId göndermeyebilir — token'dan bağla
            if (string.IsNullOrWhiteSpace(job.DomainId))
            {
                job.DomainId = domainId;
            }
            else if (!string.Equals(job.DomainId, domainId, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException(
                    $"Job belongs to different domain. JobDomainId: {job.DomainId}, UserDomainId: {domainId}");
            }

            // Validation
            ValidateJob(job);

            // Ensure job type is User
            job.JobType = JobType.User;
            job.DomainId = domainId;

            // Ensure POST payload
            job.EnsurePostPayload();

            var updatedJob = await _repository.UpdateJobAsync(domainId, job, token);

            // Trigger immediate sync
            await _syncService.SyncNowAsync();

            _logger.LogInformation("Updated user job: {JobId} in domain {DomainId}", job.JobId, domainId);
            return updatedJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(string jobId, string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            // Verify job belongs to user's domain
            var existingJob = await _repository.GetJobByIdAsync(domainId, jobId, token);
            if (existingJob == null)
            {
                return false;
            }

            if (existingJob.DomainId != domainId)
            {
                throw new UnauthorizedAccessException($"Job belongs to different domain. JobDomainId: {existingJob.DomainId}, UserDomainId: {domainId}");
            }

            var deleted = await _repository.DeleteJobAsync(domainId, jobId, token);

            if (deleted)
            {
                // Trigger immediate sync
                await _syncService.SyncNowAsync();
                _logger.LogInformation("Deleted user job: {JobId} from domain {DomainId}", jobId, domainId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user job: {JobId}", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<JobExecution>> GetJobExecutionsAsync(string jobId, int limit = 100, string? token = null)
    {
        try
        {
            var domainId = GetDomainIdFromToken();
            if (string.IsNullOrEmpty(domainId))
            {
                throw new UnauthorizedAccessException("Domain information not found in token");
            }

            return await _executionRepository.GetUserJobExecutionsAsync(domainId, jobId, limit, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution history for user job: {JobId}", jobId);
            throw;
        }
    }

    private string? GetUserIdFromToken()
    {
        try
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return null;
            }

            // Try "sub" claim first
            var userId = user.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                return userId;
            }

            // Fallback to NameIdentifier
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting user ID from token");
            return null;
        }
    }

    private void ValidateJob(ScheduledJob job)
    {
        if (string.IsNullOrWhiteSpace(job.JobId))
        {
            throw new ArgumentException("JobId is required", nameof(job));
        }

        if (string.IsNullOrWhiteSpace(job.Name))
        {
            throw new ArgumentException("Name is required", nameof(job));
        }

        if (string.IsNullOrWhiteSpace(job.CronExpression))
        {
            throw new ArgumentException("CronExpression is required", nameof(job));
        }

        if (string.IsNullOrWhiteSpace(job.EndpointUrl))
        {
            throw new ArgumentException("EndpointUrl is required", nameof(job));
        }

        if (string.IsNullOrWhiteSpace(job.HttpMethod))
        {
            throw new ArgumentException("HttpMethod is required", nameof(job));
        }

        if (!job.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            !job.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("HttpMethod must be GET or POST", nameof(job));
        }

        // Validate cron expression (basic check - Quartz will validate more thoroughly)
        if (job.CronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 6)
        {
            throw new ArgumentException("Invalid cron expression format", nameof(job));
        }
    }
}
