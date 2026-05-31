namespace MngOperations.Application.Contracts.Runtime;

using System.Text.Json;

public sealed class ProfileRuntimeContext
{
    public required string WorkspaceId { get; init; }
    public required WorkItemSummaryDto WorkItem { get; init; }
    public RuntimePermissionsDto Permissions { get; init; } = new();
    public IReadOnlyList<ProfileActionDto> Actions { get; init; } = Array.Empty<ProfileActionDto>();
    public string? ProfileId { get; init; }
    public string? ProfileName { get; init; }
    public JsonElement? Header { get; init; }
    public JsonElement? Sidebar { get; init; }
    public JsonElement? Panels { get; init; }
    public JsonElement? Layout { get; init; }
    public IReadOnlyDictionary<string, FormFieldRuntimeDto> Fields { get; init; }
        = new Dictionary<string, FormFieldRuntimeDto>();
    public IReadOnlyDictionary<string, FieldBehaviorDto> FieldBehaviors { get; init; }
        = new Dictionary<string, FieldBehaviorDto>();
    public SlaSnapshotDto? Sla { get; init; }
    public IReadOnlyList<string> Watchers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<WorkItemLinkSummaryDto> Links { get; init; } = Array.Empty<WorkItemLinkSummaryDto>();
    public IReadOnlyList<StateSegmentDto> StateSegments { get; init; } = Array.Empty<StateSegmentDto>();

    /// <summary>Person id → görünen ad (assignee/reporter/createdBy/watchers) — sidebar isim çözümü.</summary>
    public IReadOnlyDictionary<string, PersonDisplayDto> People { get; init; }
        = new Dictionary<string, PersonDisplayDto>();

    /// <summary>Grup id → grup adı (assignmentGroups + personGroups tipi pool alanlar) — sidebar/alan isim çözümü.</summary>
    public IReadOnlyDictionary<string, PersonDisplayDto> Groups { get; init; }
        = new Dictionary<string, PersonDisplayDto>();

    /// <summary>op_work_items.attachments (file isArray) ham değeri — { path, file_name, file_ext, file_size, upload_person, upload_time }[].</summary>
    public JsonElement? Attachments { get; init; }
}

public sealed class WorkItemSummaryDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string StateId { get; init; }
    public string? StateFlowId { get; init; }
    public string? Category { get; init; }
    public string? WorkspaceKey { get; init; }
    public string? Assignee { get; init; }
    public string? Reporter { get; init; }
    public string? TypeId { get; init; }
    public string? BoardId { get; init; }
    public string? PriorityId { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? LastStateChangeAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}

public sealed class WorkItemLinkSummaryDto
{
    public required string Id { get; init; }
    public required string LinkType { get; init; }
    public required string Direction { get; init; }
    public required string OtherWorkItemId { get; init; }
    public string? Description { get; init; }
}

public sealed class RuntimePermissionsDto
{
    public bool CanView { get; init; }
    public bool CanEdit { get; init; }
    public bool CanComment { get; init; }
}

public sealed class ProfileActionDto
{
    public required string TransitionKey { get; init; }
    public string? Label { get; init; }
    public string? FromStateId { get; init; }
    public required string ToStateId { get; init; }
    public bool Enabled { get; init; }
    public int Order { get; init; }

    /// <summary>Bu geçiş için zorunlu alan anahtarları (akış transition.requiredFields). UI ön-toplama yapar.</summary>
    public IReadOnlyList<string> RequiredFields { get; init; } = Array.Empty<string>();
}

public sealed class TimelinePage
{
    public IReadOnlyList<TimelineEntryDto> Items { get; init; } = Array.Empty<TimelineEntryDto>();
    public int Skip { get; init; }
    public int Take { get; init; }
    public int Total { get; init; }
}

public sealed class TimelineEntryDto
{
    public required string Type { get; init; }
    public string? Id { get; init; }
    public string? Actor { get; init; }

    /// <summary>Aktör/yazar person id'si (UI'da "kendi yorumum mu?" kontrolü için; ad değil id).</summary>
    public string? ActorId { get; init; }

    public string? Text { get; init; }
    public DateTime? At { get; init; }
    public string? ActivityType { get; init; }

    /// <summary>Yorum düzenlendiyse son düzenleme zamanı (yalnızca `type='comment'`).</summary>
    public DateTime? EditedAt { get; init; }

    /// <summary>Yanıt verilen üst yorumun id'si (yalnızca `type='comment'` girdilerde; tek seviye thread).</summary>
    public string? ParentId { get; init; }

    /// <summary>Yorum ekleri (op_comments.attachments file isArray) ham değeri — { path, file_name, ... }[].</summary>
    public JsonElement? Attachments { get; init; }
}
