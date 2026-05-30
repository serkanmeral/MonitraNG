namespace MngOperations.Application.Utilities;

/// <summary>
/// op_work_items üst seviye (core) alanları — pool değerleri <see cref="ExtraFieldsKey"/> altında tutulur.
/// </summary>
public static class WorkItemCoreFields
{
    public const string ExtraFieldsKey = "extraFields";

    /// <summary>API <c>fields</c> gövdesi veya patch ile yazılabilir core kolonlar.</summary>
    public static readonly HashSet<string> WritableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "title",
        "description",
        "typeId",
        "priorityId",
        "boardId",
        "assignee",
        "impact",
        "urgency",
        "severity",
        "labels",
        "watchers",
        "reporter",
        "assignmentGroups",
        "dueDate",
        "sla",
        "slaPolicyId",
        "parentItemId",
        "attachments"
    };

    /// <summary>Sistem yönetimli veya doğrudan değiştirilemeyen kolonlar.</summary>
    public static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "__dataId",
        "key",
        "workspaceId",
        "workspaceKey",
        "stateId",
        "stateFlowId",
        "category",
        "origin",
        ExtraFieldsKey,
        "analytics",
        "createdAt",
        "createdBy",
        "updatedAt",
        "lastStateChangeAt",
        "closedAt",
        "firstClosedAt",
        "firstStartedAt",
        "currentStateDurationMs"
    };

    public static bool IsWritable(string key) => WritableKeys.Contains(key);

    public static bool IsReserved(string key) => ReservedKeys.Contains(key);
}
