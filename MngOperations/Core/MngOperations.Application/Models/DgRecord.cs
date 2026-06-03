using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngOperations.Application.Models;

public abstract class DgRecord
{
    [JsonPropertyName("__dataId")]
    public string? DataId { get; set; }
}

public sealed class WorkspaceRecord : DgRecord
{
    public string? Key { get; set; }
    public string? Name { get; set; }

    [JsonPropertyName("workItemKeyPrefix")]
    public string? WorkItemKeyPrefix { get; set; }

    [JsonPropertyName("workItemKeyFormat")]
    public string? WorkItemKeyFormat { get; set; }

    [JsonPropertyName("workItemSequenceStart")]
    public int? WorkItemSequenceStart { get; set; }

    [JsonPropertyName("defaultStateFlowId")]
    public string? DefaultStateFlowId { get; set; }

    [JsonPropertyName("viewGroups")]
    public JsonElement? ViewGroups { get; set; }

    [JsonPropertyName("editGroups")]
    public JsonElement? EditGroups { get; set; }

    [JsonPropertyName("adminGroups")]
    public JsonElement? AdminGroups { get; set; }

    [JsonPropertyName("ownerGroups")]
    public JsonElement? OwnerGroups { get; set; }

    [JsonPropertyName("enabledTypeIds")]
    public JsonElement? EnabledTypeIds { get; set; }

    [JsonPropertyName("enabledStateIds")]
    public JsonElement? EnabledStateIds { get; set; }

    [JsonPropertyName("enabledFieldIds")]
    public JsonElement? EnabledFieldIds { get; set; }

    [JsonPropertyName("enabledPriorityIds")]
    public JsonElement? EnabledPriorityIds { get; set; }

    /// <summary>Workspace metadata — <c>fieldPolicies</c>, yedek <c>enabled*Ids</c>, vb.</summary>
    public JsonElement? Settings { get; set; }

    [JsonPropertyName("slaBreachScanSchedulerJobId")]
    public string? SlaBreachScanSchedulerJobId { get; set; }

    [JsonPropertyName("slaBreachScanCronExpression")]
    public string? SlaBreachScanCronExpression { get; set; }

    [JsonPropertyName("slaBreachScanEnabled")]
    public bool? SlaBreachScanEnabled { get; set; }
}

public sealed class FormRecord : DgRecord
{
    public string? Name { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("defaultTypeId")]
    public string? DefaultTypeId { get; set; }

    [JsonPropertyName("defaultStateId")]
    public string? DefaultStateId { get; set; }

    [JsonPropertyName("defaultStateFlowId")]
    public string? DefaultStateFlowId { get; set; }

    [JsonPropertyName("isDefault")]
    public bool? IsDefault { get; set; }

    public JsonElement? Layout { get; set; }

    [JsonPropertyName("fieldBehaviors")]
    public JsonElement? FieldBehaviors { get; set; }

    [JsonPropertyName("defaultValues")]
    public JsonElement? DefaultValues { get; set; }
}

public sealed class ProfileRecord : DgRecord
{
    public string? Name { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("defaultTypeId")]
    public string? DefaultTypeId { get; set; }

    [JsonPropertyName("isDefault")]
    public bool? IsDefault { get; set; }

    public JsonElement? Layout { get; set; }

    public JsonElement? Header { get; set; }

    public JsonElement? Sidebar { get; set; }

    public JsonElement? Panels { get; set; }

    [JsonPropertyName("fieldBehaviors")]
    public JsonElement? FieldBehaviors { get; set; }

    public JsonElement? Actions { get; set; }
}

public sealed class FieldRecord : DgRecord
{
    public string? Key { get; set; }
    public string? Label { get; set; }

    /// <summary>pool | core — pool değerleri work item <c>extraFields</c> altında tutulur.</summary>
    public string? Scope { get; set; }

    [JsonPropertyName("fieldType")]
    public string? FieldType { get; set; }

    [JsonPropertyName("isSensitive")]
    public bool? IsSensitive { get; set; }

    [JsonPropertyName("viewGroups")]
    public JsonElement? ViewGroups { get; set; }

    [JsonPropertyName("editGroups")]
    public JsonElement? EditGroups { get; set; }

    [JsonPropertyName("validationRules")]
    public JsonElement? ValidationRules { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }
}

public sealed class BoardRecord : DgRecord
{
    public string? Name { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("viewType")]
    public string? ViewType { get; set; }

    [JsonPropertyName("defaultStateFlowId")]
    public string? DefaultStateFlowId { get; set; }

    [JsonPropertyName("viewGroups")]
    public JsonElement? ViewGroups { get; set; }

    [JsonPropertyName("editGroups")]
    public JsonElement? EditGroups { get; set; }

    public JsonElement? Config { get; set; }

    [JsonPropertyName("visibleFields")]
    public JsonElement? VisibleFields { get; set; }
}

public sealed class WorkItemTypeRecord : DgRecord
{
    public string? Name { get; set; }
    public string? Category { get; set; }

    [JsonPropertyName("defaultStateFlowId")]
    public string? DefaultStateFlowId { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }
}

public sealed class StateFlowRecord : DgRecord
{
    [JsonPropertyName("initialStateId")]
    public string? InitialStateId { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    public JsonElement? Transitions { get; set; }
}

public sealed class WorkItemKeyRecord : DgRecord
{
    public string? Key { get; set; }
}

public sealed class StateRecord : DgRecord
{
    public string? Name { get; set; }
    public string? Category { get; set; }

    [JsonPropertyName("isClosed")]
    public bool? IsClosed { get; set; }

    [JsonPropertyName("isInitial")]
    public bool? IsInitial { get; set; }
}

public sealed class SlaPolicyRecord : DgRecord
{
    public string? Name { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("typeId")]
    public string? TypeId { get; set; }

    [JsonPropertyName("priorityId")]
    public string? PriorityId { get; set; }

    [JsonPropertyName("responseTargetMinutes")]
    public double? ResponseTargetMinutes { get; set; }

    [JsonPropertyName("resolveTargetMinutes")]
    public double? ResolveTargetMinutes { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }

    public double? Priority { get; set; }
}

public sealed class DashboardRecord : DgRecord
{
    public string? Name { get; set; }
    public string? Description { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    public string? Scope { get; set; }
    public JsonElement? Layout { get; set; }
    public JsonElement? Widgets { get; set; }
    public JsonElement? Permissions { get; set; }

    [JsonPropertyName("isDefault")]
    public bool? IsDefault { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

public sealed class NotificationPolicyRecord : DgRecord
{
    public string? Name { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("boardId")]
    public string? BoardId { get; set; }

    [JsonPropertyName("typeId")]
    public string? TypeId { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    public JsonElement? Channels { get; set; }
    public JsonElement? Recipients { get; set; }

    [JsonPropertyName("emailTemplateKey")]
    public string? EmailTemplateKey { get; set; }

    [JsonPropertyName("notificationTemplateKey")]
    public string? NotificationTemplateKey { get; set; }

    [JsonPropertyName("excludeActor")]
    public bool? ExcludeActor { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }

    public double? Priority { get; set; }
}

public sealed class ActivityRecord : DgRecord
{
    [JsonPropertyName("sourceDataset")]
    public string? SourceDataset { get; set; }

    [JsonPropertyName("sourceRecordId")]
    public string? SourceRecordId { get; set; }

    [JsonPropertyName("activityType")]
    public string? ActivityType { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    public string? Actor { get; set; }

    public string? Summary { get; set; }
}

public sealed class WorkItemScheduleRecord : DgRecord
{
    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; }

    public string? Timezone { get; set; }

    [JsonPropertyName("boardId")]
    public string? BoardId { get; set; }

    [JsonPropertyName("typeId")]
    public string? TypeId { get; set; }

    public string? Assignee { get; set; }

    [JsonPropertyName("priorityId")]
    public string? PriorityId { get; set; }

    public string? Title { get; set; }

    [JsonPropertyName("templateDescription")]
    public string? TemplateDescription { get; set; }

    public JsonElement? Fields { get; set; }

    [JsonPropertyName("initialTransitionKey")]
    public string? InitialTransitionKey { get; set; }

    [JsonPropertyName("schedulerJobId")]
    public string? SchedulerJobId { get; set; }

    [JsonPropertyName("lastRunAt")]
    public DateTime? LastRunAt { get; set; }

    [JsonPropertyName("lastWorkItemId")]
    public string? LastWorkItemId { get; set; }
}
