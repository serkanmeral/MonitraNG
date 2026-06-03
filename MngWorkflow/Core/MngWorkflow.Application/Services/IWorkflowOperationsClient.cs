using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Execution;

namespace MngWorkflow.Application.Services;

public interface IWorkflowOperationsClient
{
    Task<WorkflowCreateWorkItemResponse> CreateFromOriginAsync(
        string bearerToken,
        WorkflowCreateFromOriginRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowTransitionWorkItemResponse> ApplyTransitionAsync(
        string bearerToken,
        string workItemId,
        string transitionKey,
        WorkflowTransitionWorkItemRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowWorkItemDto> PatchWorkItemAsync(
        string bearerToken,
        string workItemId,
        WorkflowPatchWorkItemRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowContextTemplateResolver
{
    string Resolve(WorkflowExecutionContext context, string domainName, string template);

    string? ResolveOptional(WorkflowExecutionContext context, string domainName, string? template);
}
