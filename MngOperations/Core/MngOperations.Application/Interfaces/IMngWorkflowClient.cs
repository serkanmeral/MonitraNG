using MngOperations.Application.Contracts.Workflow;

namespace MngOperations.Application.Interfaces;

public interface IMngWorkflowClient
{
    Task<StartWorkflowRunResponse> StartRunAsync(
        string domainName,
        string bearerToken,
        StartWorkflowRunRequest request,
        CancellationToken cancellationToken = default);
}
