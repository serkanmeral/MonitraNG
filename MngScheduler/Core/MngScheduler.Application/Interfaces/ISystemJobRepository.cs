using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Repository interface for System Job operations
/// System jobs are stored in mng_keeper database
/// </summary>
public interface ISystemJobRepository
{
    /// <summary>
    /// Get all active system jobs
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetActiveJobsAsync();

    /// <summary>
    /// Get job by ID
    /// </summary>
    Task<ScheduledJob?> GetJobByIdAsync(string jobId);

    /// <summary>
    /// Get all jobs (active and inactive)
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetAllJobsAsync();

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
    /// Check if job exists by jobId
    /// </summary>
    Task<bool> JobExistsAsync(string jobId);
}
