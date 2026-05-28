using MngOperations.Application.Contracts.Schedules;

namespace MngOperations.Application.Interfaces;

public interface IMngSchedulerClient
{
    Task<SchedulerUserJobDto?> GetUserJobAsync(string jobId, string bearerToken, CancellationToken cancellationToken = default);

    Task<SchedulerUserJobDto> CreateUserJobAsync(
        SchedulerUserJobDto job,
        string bearerToken,
        CancellationToken cancellationToken = default);

    Task<SchedulerUserJobDto> UpdateUserJobAsync(
        SchedulerUserJobDto job,
        string bearerToken,
        CancellationToken cancellationToken = default);

    Task DeleteUserJobAsync(string jobId, string bearerToken, CancellationToken cancellationToken = default);
}
