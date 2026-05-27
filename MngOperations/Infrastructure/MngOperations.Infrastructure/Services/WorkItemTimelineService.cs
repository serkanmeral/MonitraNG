using Microsoft.Extensions.Logging;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public class WorkItemTimelineService : IWorkItemTimelineService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<WorkItemTimelineService> _logger;

    public WorkItemTimelineService(
        IMngDataGatewayClient dg,
        IRequestContext requestContext,
        ILogger<WorkItemTimelineService> logger)
    {
        _dg = dg;
        _requestContext = requestContext;
        _logger = logger;
    }

    public Task OpenInitialSegmentAsync(
        string workItemId,
        string stateId,
        DateTime enteredAtUtc,
        string? assignee,
        string token,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false) =>
        CreateSegmentAsync(
            workItemId,
            fromStateId: null,
            toStateId: stateId,
            transitionKey: null,
            enteredAtUtc,
            assignee,
            token,
            throwOnFailure,
            cancellationToken);

    public async Task RecordTransitionAsync(
        string workItemId,
        string fromStateId,
        string toStateId,
        string transitionKey,
        DateTime enteredAtUtc,
        string? assignee,
        string token,
        CancellationToken cancellationToken = default,
        bool throwOnFailure = false)
    {
        await CloseOpenSegmentAsync(workItemId, enteredAtUtc, token, throwOnFailure, cancellationToken);

        await CreateSegmentAsync(
            workItemId,
            fromStateId,
            toStateId,
            transitionKey,
            enteredAtUtc,
            assignee,
            token,
            throwOnFailure,
            cancellationToken);
    }

    private async Task CloseOpenSegmentAsync(
        string workItemId,
        DateTime leftAtUtc,
        string token,
        bool throwOnFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            var openSegment = await FindOpenSegmentAsync(workItemId, token, cancellationToken);
            if (openSegment == null)
                return;

            var segmentId = WorkItemDataHelper.GetDataId(openSegment);
            if (string.IsNullOrEmpty(segmentId))
                return;

            var enteredAt = WorkItemDataHelper.GetDateTime(openSegment, "enteredAt") ?? leftAtUtc;
            var durationMs = Math.Max(0, (long)(leftAtUtc - enteredAt).TotalMilliseconds);

            var patch = new Dictionary<string, object?>(openSegment, StringComparer.OrdinalIgnoreCase)
            {
                ["leftAt"] = leftAtUtc,
                ["durationMs"] = durationMs
            };
            patch.Remove("__dataId");

            await _dg.UpdateAsync(OcDatasets.WorkItemTimelines, segmentId, patch, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Timeline close segment failed for work item {WorkItemId} (non-fatal)", workItemId);
            if (throwOnFailure)
                throw;
        }
    }

    private async Task CreateSegmentAsync(
        string workItemId,
        string? fromStateId,
        string toStateId,
        string? transitionKey,
        DateTime enteredAtUtc,
        string? assignee,
        string token,
        bool throwOnFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["workItemId"] = workItemId,
                ["toStateId"] = toStateId,
                ["enteredAt"] = enteredAtUtc,
                ["changedBy"] = _requestContext.Username,
                ["assigneeAtThatTime"] = assignee
            };

            if (!string.IsNullOrEmpty(fromStateId))
                payload["fromStateId"] = fromStateId;

            if (!string.IsNullOrEmpty(transitionKey))
                payload["transitionKey"] = transitionKey;

            await _dg.CreateAsync(OcDatasets.WorkItemTimelines, payload, token, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Timeline segment write failed for work item {WorkItemId} (non-fatal)", workItemId);
            if (throwOnFailure)
                throw;
        }
    }

    private async Task<Dictionary<string, object?>?> FindOpenSegmentAsync(
        string workItemId,
        string token,
        CancellationToken cancellationToken)
    {
        var filter = $"workItemId:eq:{workItemId}";
        var segments = await _dg.GetAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItemTimelines,
            $"filter={Uri.EscapeDataString(filter)}&limit=200",
            token,
            cancellationToken);

        return segments
            .Where(s => WorkItemDataHelper.GetDateTime(s, "leftAt") == null)
            .OrderByDescending(s => WorkItemDataHelper.GetDateTime(s, "enteredAt") ?? DateTime.MinValue)
            .FirstOrDefault();
    }
}
