namespace MngOperations.Application.Interfaces;

/// <summary>
/// op_work_item_timelines segment yönetimi — açık segment kapatma + yeni segment açma.
/// </summary>
public interface IWorkItemTimelineService
{
    Task OpenInitialSegmentAsync(
        string workItemId,
        string stateId,
        DateTime enteredAtUtc,
        string? assignee,
        string token,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false);

    Task RecordTransitionAsync(
        string workItemId,
        string fromStateId,
        string toStateId,
        string transitionKey,
        DateTime enteredAtUtc,
        string? assignee,
        string token,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false);
}
