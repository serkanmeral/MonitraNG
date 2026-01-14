using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Service interface for User Job operations
/// User jobs are domain-specific and stored in domain databases via MngDataGateway
/// </summary>
public interface IUserJobService
{
    /// <summary>
    /// Get all user jobs for the current user's domain
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetAllJobsAsync(string? token = null);

    /// <summary>
    /// Get active user jobs for the current user's domain
    /// </summary>
    Task<IEnumerable<ScheduledJob>> GetActiveJobsAsync(string? token = null);

    /// <summary>
    /// Get job by ID for the current user's domain
    /// </summary>
    Task<ScheduledJob?> GetJobByIdAsync(string jobId, string? token = null);

    /// <summary>
    /// Create a new user job in the current user's domain
    /// </summary>
    Task<ScheduledJob> CreateJobAsync(ScheduledJob job, string? token = null);

    /// <summary>
    /// Update an existing user job in the current user's domain
    /// </summary>
    Task<ScheduledJob> UpdateJobAsync(ScheduledJob job, string? token = null);

    /// <summary>
    /// Delete a user job from the current user's domain
    /// </summary>
    Task<bool> DeleteJobAsync(string jobId, string? token = null);

    /// <summary>
    /// Get execution history for a job in the current user's domain
    /// </summary>
    Task<IEnumerable<JobExecution>> GetJobExecutionsAsync(string jobId, int limit = 100, string? token = null);

    /// <summary>
    /// Get domain ID from token (JWT claim)
    /// </summary>
    string? GetDomainIdFromToken();
}
