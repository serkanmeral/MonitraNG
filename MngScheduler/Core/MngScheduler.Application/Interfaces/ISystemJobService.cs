using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Service interface for System Job operations
/// System jobs are admin-only and stored in mng_keeper database
/// </summary>
public interface ISystemJobService
{
    /// <summary>
    /// Get all system jobs (active and inactive)
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetAllJobsAsync();

    /// <summary>
    /// Get active system jobs only
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetActiveJobsAsync();

    /// <summary>
    /// Get job by ID
    /// </summary>
    Task<ScheduledJob?> GetJobByIdAsync(string jobId);

    /// <summary>
    /// Create a new system job
    /// </summary>
    Task<ScheduledJob> CreateJobAsync(ScheduledJob job);

    /// <summary>
    /// Update an existing system job
    /// </summary>
    Task<ScheduledJob> UpdateJobAsync(ScheduledJob job);

    /// <summary>
    /// Delete a system job
    /// </summary>
    Task<bool> DeleteJobAsync(string jobId);

    /// <summary>
    /// Get execution history for a job
    /// </summary>
    Task<IEnumerable<JobExecution>> GetJobExecutionsAsync(string jobId, int limit = 100);
}
