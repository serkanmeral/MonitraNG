using MngOperations.Application.Contracts.WorkItems;

namespace MngOperations.Application.Interfaces;

public interface IWorkItemCommandService
{
    Task<CreateWorkItemResponse> CreateAsync(CreateWorkItemRequest request, CancellationToken cancellationToken = default);

    Task<CreateWorkItemResponse> CreateFromOriginAsync(
        CreateFromOriginRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkItemDto> PatchAsync(
        string workItemId,
        PatchWorkItemRequest request,
        CancellationToken cancellationToken = default);

    Task<TransitionWorkItemResponse> ApplyTransitionAsync(
        string workItemId,
        string transitionKey,
        TransitionWorkItemRequest request,
        CancellationToken cancellationToken = default);

    Task<CommentDto> AddCommentAsync(
        string workItemId,
        AddCommentRequest request,
        CancellationToken cancellationToken = default);
}
