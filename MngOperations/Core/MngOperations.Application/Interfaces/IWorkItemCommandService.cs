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

    /// <summary>Yorum gövdesini günceller. Yetki: yalnızca yorumun yazarı (aksi 403).</summary>
    Task<CommentDto> UpdateCommentAsync(
        string workItemId,
        string commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Yorumu siler. Yetki: yalnızca yorumun yazarı (aksi 403).</summary>
    Task DeleteCommentAsync(
        string workItemId,
        string commentId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string workItemId,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task RunAutomationRulesAsync(
        string workItemId,
        string trigger,
        CancellationToken cancellationToken = default);
}
