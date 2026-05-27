using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Utilities;

namespace MngOperations.Application.Utilities;

public static class ProfileRuntimeMapper
{
    public const int DefaultStateSegmentCount = 5;

    public static StateSegmentDto MapStateSegment(IReadOnlyDictionary<string, object?> row)
    {
        var toStateId = WorkItemDataHelper.GetString(row, "toStateId") ?? string.Empty;
        var enteredAt = WorkItemDataHelper.GetDateTime(row, "enteredAt") ?? DateTime.MinValue;

        return new StateSegmentDto
        {
            Id = WorkItemDataHelper.GetDataId(row),
            FromStateId = WorkItemDataHelper.GetString(row, "fromStateId"),
            ToStateId = toStateId,
            EnteredAt = enteredAt,
            LeftAt = WorkItemDataHelper.GetDateTime(row, "leftAt"),
            DurationMs = ReadLong(row, "durationMs"),
            TransitionKey = WorkItemDataHelper.GetString(row, "transitionKey"),
            ChangedBy = WorkItemDataHelper.GetString(row, "changedBy"),
            AssigneeAtThatTime = WorkItemDataHelper.GetString(row, "assigneeAtThatTime")
        };
    }

    public static WorkItemLinkSummaryDto MapOutgoingLink(IReadOnlyDictionary<string, object?> row) =>
        new()
        {
            Id = WorkItemDataHelper.GetDataId(row),
            LinkType = WorkItemDataHelper.GetString(row, "linkType") ?? "relates_to",
            Direction = "outgoing",
            OtherWorkItemId = WorkItemDataHelper.GetString(row, "targetWorkItemId") ?? string.Empty,
            Description = WorkItemDataHelper.GetString(row, "description")
        };

    public static WorkItemLinkSummaryDto MapIncomingLink(IReadOnlyDictionary<string, object?> row) =>
        new()
        {
            Id = WorkItemDataHelper.GetDataId(row),
            LinkType = WorkItemDataHelper.GetString(row, "linkType") ?? "relates_to",
            Direction = "incoming",
            OtherWorkItemId = WorkItemDataHelper.GetString(row, "sourceWorkItemId") ?? string.Empty,
            Description = WorkItemDataHelper.GetString(row, "description")
        };

    private static long? ReadLong(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n) => n,
            _ => null
        };
    }
}
