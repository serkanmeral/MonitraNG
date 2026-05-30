using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Utilities;

public static class ProfileRuntimeBuilder
{
    private static readonly (string Key, string Label, string FieldType)[] ProfileFields =
    [
        ("key", "Key", "text"),
        ("title", "Title", "text"),
        ("description", "Description", "text"),
        ("typeId", "Type", "relation"),
        ("stateId", "State", "relation"),
        ("assignee", "Assignee", "persons"),
        ("reporter", "Reporter", "persons"),
        ("priorityId", "Priority", "relation"),
        ("boardId", "Board", "relation"),
        ("category", "Category", "text")
    ];

    public static IReadOnlyDictionary<string, FormFieldRuntimeDto> BuildFields(
        IReadOnlyDictionary<string, object?> workItem)
    {
        var fields = new Dictionary<string, FormFieldRuntimeDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, label, fieldType) in ProfileFields)
        {
            var value = WorkItemDataHelper.GetFieldValue(workItem, key);
            fields[key] = new FormFieldRuntimeDto
            {
                Key = key,
                Label = label,
                FieldType = fieldType,
                Value = FormRuntimeBuilderNormalize(value)
            };
        }

        return fields;
    }

    public static WorkItemSummaryDto BuildSummary(string workItemId, IReadOnlyDictionary<string, object?> workItem)
    {
        var boardId = WorkItemDataHelper.GetString(workItem, "boardId");

        return new WorkItemSummaryDto
        {
            Id = workItemId,
            Key = WorkItemDataHelper.GetString(workItem, "key") ?? workItemId,
            Title = WorkItemDataHelper.GetString(workItem, "title") ?? string.Empty,
            Description = WorkItemDataHelper.GetString(workItem, "description"),
            StateId = WorkItemDataHelper.GetString(workItem, "stateId") ?? string.Empty,
            StateFlowId = WorkItemDataHelper.GetString(workItem, "stateFlowId"),
            Category = WorkItemDataHelper.GetString(workItem, "category"),
            WorkspaceKey = WorkItemDataHelper.GetString(workItem, "workspaceKey"),
            Assignee = WorkItemDataHelper.GetString(workItem, "assignee"),
            Reporter = WorkItemDataHelper.GetString(workItem, "reporter"),
            TypeId = WorkItemDataHelper.GetString(workItem, "typeId"),
            BoardId = boardId,
            PriorityId = WorkItemDataHelper.GetString(workItem, "priorityId"),
            CreatedAt = WorkItemDataHelper.GetDateTime(workItem, "createdAt"),
            CreatedBy = WorkItemDataHelper.GetString(workItem, "createdBy"),
            LastStateChangeAt = WorkItemDataHelper.GetDateTime(workItem, "lastStateChangeAt"),
            ClosedAt = WorkItemDataHelper.GetDateTime(workItem, "closedAt")
        };
    }

    private static object? FormRuntimeBuilderNormalize(object? value)
    {
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object?>(el.GetRawText())
            };
        }

        return value;
    }
}
