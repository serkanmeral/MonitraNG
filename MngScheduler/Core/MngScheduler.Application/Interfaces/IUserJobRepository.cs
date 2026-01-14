using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Repository interface for User Job operations
/// User jobs are stored in domain databases via MngDataGateway dataset API
/// </summary>
public interface IUserJobRepository
{
    /// <summary>
    /// Get active jobs for a specific domain
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetActiveJobsByDomainAsync(string domainId, string? token = null);

    /// <summary>
    /// Get all active jobs from all active domains (parallel reading)
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetAllActiveJobsAsync(string? token = null);

    /// <summary>
    /// Get job by ID from a specific domain
    /// </summary>
    Task<ScheduledJob?> GetJobByIdAsync(string domainId, string jobId, string? token = null);

    /// <summary>
    /// Get all jobs (active and inactive) for a specific domain
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetJobsByDomainAsync(string domainId, string? token = null);

    /// <summary>
    /// Create a new user job in a specific domain
    /// </summary>
    Task<ScheduledJob> CreateJobAsync(string domainId, ScheduledJob job, string? token = null);

    /// <summary>
    /// Update an existing user job in a specific domain
    /// </summary>
    Task<ScheduledJob> UpdateJobAsync(string domainId, ScheduledJob job, string? token = null);

    /// <summary>
    /// Delete a user job from a specific domain
    /// </summary>
    Task<bool> DeleteJobAsync(string domainId, string jobId, string? token = null);

    /// <summary>
    /// Check if job exists by jobId in a specific domain
    /// </summary>
    Task<bool> JobExistsAsync(string domainId, string jobId, string? token = null);
}
