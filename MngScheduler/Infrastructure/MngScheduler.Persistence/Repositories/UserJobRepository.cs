using Microsoft.Extensions.Logging;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;
using System.Text.Json;

namespace MngScheduler.Persistence.Repositories;

/// <summary>
/// Repository implementation for User Job operations
/// User jobs are stored in domain databases → @scheduled_jobs dataset (MngDataGateway API)
/// </summary>
public class UserJobRepository : IUserJobRepository
{
    private readonly IMngDataGatewayClient _dataGatewayClient;
    private readonly IDomainLookupService _domainLookupService;
    private readonly ILogger<UserJobRepository> _logger;
    private const string DatasetName = "@scheduled_jobs";

    public UserJobRepository(
        IMngDataGatewayClient dataGatewayClient,
        IDomainLookupService domainLookupService,
        ILogger<UserJobRepository> logger)
    {
        _dataGatewayClient = dataGatewayClient ?? throw new ArgumentNullException(nameof(dataGatewayClient));
        _domainLookupService = domainLookupService ?? throw new ArgumentNullException(nameof(domainLookupService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ScheduledJob>> GetActiveJobsByDomainAsync(string domainId, string? token = null)
    {
        try
        {
            // Query: jobType = "User" AND isActive = true AND domainId = {domainId}
            // Note: StartDate, ExpireDate, MaxExecutionCount checks are done in ShouldExecute() method
            var query = $"filter=jobType:User,isActive:true,domainId:{domainId}";
            var jobs = await _dataGatewayClient.GetAsync<ScheduledJob>(DatasetName, query, token);
            
            var now = DateTime.UtcNow;
            var jobsList = jobs.ToList();
            
            // Additional runtime checks: filter out jobs that shouldn't execute
            // and auto-deactivate expired jobs or jobs that reached execution limit
            var jobsToDeactivate = new List<ScheduledJob>();
            var validJobs = new List<ScheduledJob>();
            
            foreach (var job in jobsList)
            {
                if (job.ShouldExecute(now))
                {
                    validJobs.Add(job);
                }
                else if (!job.IsActive)
                {
                    // Job was auto-deactivated by ShouldExecute() (expired or limit reached)
                    jobsToDeactivate.Add(job);
                }
            }
            
            // Auto-deactivate expired jobs or jobs that reached execution limit
            if (jobsToDeactivate.Any())
            {
                var deactivateTasks = jobsToDeactivate.Select(job => 
                    UpdateJobAsync(domainId, job, token).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogWarning(t.Exception, "Error auto-deactivating user job {JobId} in domain {DomainId}", 
                                job.JobId, domainId);
                        }
                        else
                        {
                            _logger.LogInformation("Auto-deactivated user job {JobId} in domain {DomainId} (expired or execution limit reached)", 
                                job.JobId, domainId);
                        }
                    })
                );
                await Task.WhenAll(deactivateTasks);
            }
            
            _logger.LogDebug("Retrieved {Count} active user jobs for domain {DomainId} (filtered from {TotalCount})", 
                validJobs.Count, jobsList.Count, domainId);
            return validJobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active user jobs for domain {DomainId}", domainId);
            throw;
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetAllActiveJobsAsync(string? token = null)
    {
        try
        {
            // Get all active domains
            var activeDomains = await _domainLookupService.GetActiveDomainsAsync();
            var domainList = activeDomains.ToList();

            if (!domainList.Any())
            {
                _logger.LogDebug("No active domains found");
                return Enumerable.Empty<ScheduledJob>();
            }

            // Read jobs from all domains in parallel
            var tasks = domainList.Select(domain => 
                GetActiveJobsByDomainAsync(domain.Id, token)
                    .ContinueWith(t => 
                    {
                        if (t.IsFaulted)
                        {
                            _logger.LogWarning(t.Exception, "Error reading jobs from domain {DomainId}", domain.Id);
                            return Enumerable.Empty<ScheduledJob>();
                        }
                        return t.Result;
                    })
            );

            var results = await Task.WhenAll(tasks);
            var allJobs = results.SelectMany(x => x).ToList();

            _logger.LogDebug("Retrieved {Count} active user jobs from {DomainCount} domains", 
                allJobs.Count, domainList.Count);

            return allJobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all active user jobs");
            throw;
        }
    }

    public async Task<ScheduledJob?> GetJobByIdAsync(string domainId, string jobId, string? token = null)
    {
        try
        {
            // First, try to get by query (more efficient if MngDataGateway supports it)
            // Otherwise, we might need to get all and filter
            var query = $"filter=jobId:{jobId},domainId:{domainId}";
            var jobs = await _dataGatewayClient.GetAsync<ScheduledJob>(DatasetName, query, token);
            
            var job = jobs.FirstOrDefault();
            if (job != null)
            {
                _logger.LogDebug("Retrieved user job {JobId} from domain {DomainId}", jobId, domainId);
            }
            else
            {
                _logger.LogDebug("User job {JobId} not found in domain {DomainId}", jobId, domainId);
            }

            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user job {JobId} from domain {DomainId}", jobId, domainId);
            throw;
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetJobsByDomainAsync(string domainId, string? token = null)
    {
        try
        {
            // Query: jobType = "User" AND domainId = {domainId}
            var query = $"filter=jobType:User,domainId:{domainId}";
            var jobs = await _dataGatewayClient.GetAsync<ScheduledJob>(DatasetName, query, token);
            
            _logger.LogDebug("Retrieved {Count} user jobs for domain {DomainId}", jobs.Count(), domainId);
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user jobs for domain {DomainId}", domainId);
            throw;
        }
    }

    public async Task<ScheduledJob> CreateJobAsync(string domainId, ScheduledJob job, string? token = null)
    {
        try
        {
            // Ensure job type is User and domainId is set
            job.JobType = JobType.User;
            job.DomainId = domainId;
            job.CreatedAt = DateTime.UtcNow;
            job.UpdatedAt = null;

            // Ensure POST requests have default payload if not provided
            job.EnsurePostPayload();

            var createdJob = await _dataGatewayClient.CreateAsync<ScheduledJob>(DatasetName, job, token);

            if (string.IsNullOrWhiteSpace(createdJob.GetRecordId()))
            {
                var reloaded = await GetJobByIdAsync(domainId, job.JobId, token);
                if (reloaded != null)
                    createdJob = reloaded;
            }
            
            _logger.LogInformation("Created user job {JobId} in domain {DomainId}", job.JobId, domainId);
            return createdJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user job {JobId} in domain {DomainId}", job.JobId, domainId);
            throw;
        }
    }

    public async Task<ScheduledJob> UpdateJobAsync(string domainId, ScheduledJob job, string? token = null)
    {
        try
        {
            // Ensure domainId matches
            if (job.DomainId != domainId)
            {
                throw new ArgumentException($"Job domainId ({job.DomainId}) does not match provided domainId ({domainId})");
            }

            // MngDataGateway uses _id for updates, but we need to find the document first
            // Get the job to find its _id and preserve execution counts
            var existingJob = await GetJobByIdAsync(domainId, job.JobId, token);
            if (existingJob == null)
            {
                throw new Domain.Exceptions.JobNotFoundException(job.JobId);
            }

            // Preserve execution counts — execution handlers increment before update; keep the higher values.
            job.TotalExecutionCount = Math.Max(job.TotalExecutionCount, existingJob.TotalExecutionCount);
            job.SuccessfulExecutionCount = Math.Max(job.SuccessfulExecutionCount, existingJob.SuccessfulExecutionCount);
            job.FailedExecutionCount = Math.Max(job.FailedExecutionCount, existingJob.FailedExecutionCount);
            job.CreatedAt = existingJob.CreatedAt;
            job.CreatedBy = existingJob.CreatedBy;
            if (job.LastExecution == null)
            {
                job.LastExecution = existingJob.LastExecution;
            }
            else if (existingJob.LastExecution != null &&
                     existingJob.LastExecution.ExecutedAt > job.LastExecution.ExecutedAt)
            {
                job.LastExecution = existingJob.LastExecution;
            }
            job.Id = existingJob.Id; // Preserve MongoDB ObjectId
            job.DataId = existingJob.DataId ?? existingJob.GetRecordId();

            job.UpdatedAt = DateTime.UtcNow;

            // Ensure POST requests have default payload if not provided
            job.EnsurePostPayload();

            var recordId = existingJob.GetRecordId();
            if (string.IsNullOrWhiteSpace(recordId))
            {
                _logger.LogWarning(
                    "User job {JobId} has no DG record id (__dataId); skipping DG update (execution counters not persisted)",
                    job.JobId);
                return job;
            }

            var updatedJob = await _dataGatewayClient.UpdateAsync<ScheduledJob>(
                DatasetName, 
                recordId, 
                job, 
                token);

            _logger.LogInformation("Updated user job {JobId} in domain {DomainId}", job.JobId, domainId);
            return updatedJob;
        }
        catch (Domain.Exceptions.JobNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user job {JobId} in domain {DomainId}", job.JobId, domainId);
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(string domainId, string jobId, string? token = null)
    {
        try
        {
            // Get the job to find its _id
            var existingJob = await GetJobByIdAsync(domainId, jobId, token);
            if (existingJob == null)
            {
                _logger.LogWarning("User job {JobId} not found in domain {DomainId} for deletion", jobId, domainId);
                return false;
            }

            var recordId = existingJob.GetRecordId();
            if (string.IsNullOrWhiteSpace(recordId))
            {
                _logger.LogWarning(
                    "User job {JobId} has no DG record id (__dataId); skipping DG delete",
                    jobId);
                return false;
            }

            var deleted = await _dataGatewayClient.DeleteAsync(DatasetName, recordId, token);
            
            if (deleted)
            {
                _logger.LogInformation("Deleted user job {JobId} from domain {DomainId}", jobId, domainId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user job {JobId} from domain {DomainId}", jobId, domainId);
            throw;
        }
    }

    public async Task<bool> JobExistsAsync(string domainId, string jobId, string? token = null)
    {
        try
        {
            var job = await GetJobByIdAsync(domainId, jobId, token);
            return job != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user job exists: {JobId} in domain {DomainId}", jobId, domainId);
            throw;
        }
    }
}
