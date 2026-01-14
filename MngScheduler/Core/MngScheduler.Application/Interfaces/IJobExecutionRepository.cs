using MngScheduler.Domain.Entities;

namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Repository interface for Job Execution history operations
/// </summary>
public interface IJobExecutionRepository
{
    /// <summary>
    /// Save execution history for a system job
    /// </summary>
    Task<JobExecution> SaveSystemJobExecutionAsync(JobExecution execution);

    /// <summary>
    /// Save execution history for a user job
    /// </summary>
    Task<JobExecution> SaveUserJobExecutionAsync(string domainId, JobExecution execution, string? token = null);

    /// <summary>
    /// Get execution history for a system job
    /// </summary>
    Task<IEnumerable<JobExecution>> GetSystemJobExecutionsAsync(string jobId, int limit = 100);

    /// <summary>
    /// Get execution history for a user job
    /// </summary>
    Task<IEnumerable<JobExecution>> GetUserJobExecutionsAsync(string domainId, string jobId, int limit = 100, string? token = null);

    /// <summary>
    /// Cleanup old executions (TTL cleanup)
    /// </summary>
    Task<int> CleanupOldExecutionsAsync(TimeSpan retentionPeriod);
}
