using Microsoft.Extensions.Logging;
using MngScheduler.Application.Interfaces;
using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Services;

/// <summary>
/// Service implementation for System Job operations
/// </summary>
public class SystemJobService : ISystemJobService
{
    private readonly ISystemJobRepository _repository;
    private readonly IJobExecutionRepository _executionRepository;
    private readonly IJobSyncService _syncService;
    private readonly ILogger<SystemJobService> _logger;

    public SystemJobService(
        ISystemJobRepository repository,
        IJobExecutionRepository executionRepository,
        IJobSyncService syncService,
        ILogger<SystemJobService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _executionRepository = executionRepository ?? throw new ArgumentNullException(nameof(executionRepository));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<ScheduledJob>> GetAllJobsAsync()
    {
        try
        {
            return await _repository.GetAllJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all system jobs");
            throw;
        }
    }

    public async Task<IEnumerable<ScheduledJob>> GetActiveJobsAsync()
    {
        try
        {
            return await _repository.GetActiveJobsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active system jobs");
            throw;
        }
    }

    public async Task<ScheduledJob?> GetJobByIdAsync(string jobId)
    {
        try
        {
            return await _repository.GetJobByIdAsync(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system job by ID: {JobId}", jobId);
            throw;
        }
    }

    public async Task<ScheduledJob> CreateJobAsync(ScheduledJob job)
    {
        try
        {
            // Validation
            ValidateJob(job);

            // Ensure job type is System
            job.JobType = JobType.System;
            job.DomainId = null; // System jobs don't have domain
            job.CreatedBy = null; // Can be set from token if needed

            // Ensure POST payload
            job.EnsurePostPayload();

            var createdJob = await _repository.CreateJobAsync(job);

            // Trigger immediate sync
            await _syncService.SyncNowAsync();

            _logger.LogInformation("Created system job: {JobId}", job.JobId);
            return createdJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating system job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<ScheduledJob> UpdateJobAsync(ScheduledJob job)
    {
        try
        {
            // Validation
            ValidateJob(job);

            // Ensure job type is System
            job.JobType = JobType.System;
            job.DomainId = null; // System jobs don't have domain

            // Ensure POST payload
            job.EnsurePostPayload();

            var updatedJob = await _repository.UpdateJobAsync(job);

            // Trigger immediate sync
            await _syncService.SyncNowAsync();

            _logger.LogInformation("Updated system job: {JobId}", job.JobId);
            return updatedJob;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<bool> DeleteJobAsync(string jobId)
    {
        try
        {
            var deleted = await _repository.DeleteJobAsync(jobId);

            if (deleted)
            {
                // Trigger immediate sync
                await _syncService.SyncNowAsync();
                _logger.LogInformation("Deleted system job: {JobId}", jobId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting system job: {JobId}", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<JobExecution>> GetJobExecutionsAsync(string jobId, int limit = 100)
    {
        try
        {
            return await _executionRepository.GetSystemJobExecutionsAsync(jobId, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution history for system job: {JobId}", jobId);
            throw;
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
