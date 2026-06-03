using MngWorkflow.Application.Contracts;

namespace MngWorkflow.Application.Services;

public interface IWorkflowSchedulerClient
{
    Task<WorkflowSchedulerUserJobDto?> GetUserJobAsync(string jobId, string bearerToken, CancellationToken cancellationToken = default);
    Task<WorkflowSchedulerUserJobDto> CreateUserJobAsync(WorkflowSchedulerUserJobDto job, string bearerToken, CancellationToken cancellationToken = default);
    Task<WorkflowSchedulerUserJobDto> UpdateUserJobAsync(WorkflowSchedulerUserJobDto job, string bearerToken, CancellationToken cancellationToken = default);
    Task DeleteUserJobAsync(string jobId, string bearerToken, CancellationToken cancellationToken = default);
}

public interface IWorkflowKeeperAuthClient
{
    Task<string?> GetServiceAccessTokenAsync(CancellationToken cancellationToken = default);
}
