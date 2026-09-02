namespace MngOperations.Application.Models;

public sealed class PmProjectRow
{
    public string? __dataId { get; set; }
    public string? code { get; set; }
    public string? name { get; set; }
    public string? description { get; set; }
    public string? status { get; set; }
    public DateTime? plannedStart { get; set; }
    public DateTime? plannedFinish { get; set; }
    public DateTime? actualStart { get; set; }
    public DateTime? actualFinish { get; set; }
    public DateTime? baselineSetAt { get; set; }
    public string? baselineSetBy { get; set; }
    public string? baselineNote { get; set; }
    public string? diFolderId { get; set; }
    public string? workspaceId { get; set; }
}

public sealed class PmWbsRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? parentId { get; set; }
    public string? kind { get; set; }
    public string? name { get; set; }
    public string? wbsCode { get; set; }
    public int? sortOrder { get; set; }
    public DateTime? plannedStart { get; set; }
    public DateTime? plannedFinish { get; set; }
    public DateTime? actualStart { get; set; }
    public DateTime? actualFinish { get; set; }
    public DateTime? baselineStart { get; set; }
    public DateTime? baselineFinish { get; set; }
    public double? weight { get; set; }
    public double? percentComplete { get; set; }
    public string? workItemId { get; set; }
}

public sealed class PmDependencyRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? predecessorId { get; set; }
    public string? successorId { get; set; }
    public string? type { get; set; }
    public double? lagDays { get; set; }
}

public sealed class PmDecisionRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? title { get; set; }
    public string? body { get; set; }
    public string? kind { get; set; }
    public string? status { get; set; }
    public DateTime? decidedAt { get; set; }
    public string? decidedBy { get; set; }
    public string? documentId { get; set; }
    public List<string>? wbsIds { get; set; }
    public List<string>? workItemIds { get; set; }
    public List<string>? resourceIds { get; set; }
}

public sealed class PmProjectPackRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? packCode { get; set; }
    public string? version { get; set; }
    public DateTime? appliedAt { get; set; }
    public string? appliedBy { get; set; }
}

public sealed class PmStageGateRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? name { get; set; }
    public string? wbsId { get; set; }
    public int? sortOrder { get; set; }
    public string? status { get; set; }
    public List<string>? criteria { get; set; }
    public List<string>? satisfied { get; set; }
    public string? note { get; set; }
    public DateTime? decidedAt { get; set; }
    public string? decidedBy { get; set; }
    public List<string>? resourceIds { get; set; }
    public string? decisionId { get; set; }
}

public sealed class PmRaidItemRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? kind { get; set; }
    public string? title { get; set; }
    public string? body { get; set; }
    public string? status { get; set; }
    public string? impact { get; set; }
    public string? likelihood { get; set; }
    public string? response { get; set; }
    public string? owner { get; set; }
    public DateTime? dueDate { get; set; }
    public List<string>? wbsIds { get; set; }
    public List<string>? workItemIds { get; set; }
    public List<string>? resourceIds { get; set; }
    public DateTime? closedAt { get; set; }
    public string? closedBy { get; set; }
}

public sealed class PmResourceAssignmentRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? wbsId { get; set; }
    public string? personId { get; set; }
    public string? name { get; set; }
    public string? role { get; set; }
    public double? plannedHours { get; set; }
    public DateTime? start { get; set; }
    public DateTime? finish { get; set; }
}

public sealed class PmBudgetLineRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? wbsId { get; set; }
    public string? category { get; set; }
    public string? name { get; set; }
    public double? plannedAmount { get; set; }
    public double? actualAmount { get; set; }
    public string? currency { get; set; }
    public string? note { get; set; }
}

public sealed class PmAcknowledgementRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? resourceId { get; set; }
    public string? title { get; set; }
    public string? versionLabel { get; set; }
    public string? personName { get; set; }
    public string? personId { get; set; }
    public string? wbsId { get; set; }
    public string? status { get; set; }
    public DateTime? dueDate { get; set; }
    public string? note { get; set; }
    public DateTime? acknowledgedAt { get; set; }
    public string? acknowledgedBy { get; set; }
}

public sealed class PmObligationRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? title { get; set; }
    public string? clauseRef { get; set; }
    public string? sourceResourceId { get; set; }
    public string? wbsId { get; set; }
    public string? workItemId { get; set; }
    public string? evidenceResourceId { get; set; }
    public string? status { get; set; }
    public DateTime? dueDate { get; set; }
    public string? note { get; set; }
    public DateTime? closedAt { get; set; }
    public string? closedBy { get; set; }
}

public sealed class PmAuditPackRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? name { get; set; }
    public string? kind { get; set; }
    public string? wbsId { get; set; }
    public string? status { get; set; }
    public DateTime? dueDate { get; set; }
    public List<string>? resourceIds { get; set; }
    public string? recipient { get; set; }
    public string? note { get; set; }
    public DateTime? issuedAt { get; set; }
    public string? issuedBy { get; set; }
}

public sealed class PmMeetingRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? name { get; set; }
    public DateTime? heldAt { get; set; }
    public string? minutesResourceId { get; set; }
    public string? wbsId { get; set; }
    public string? attendees { get; set; }
    public string? note { get; set; }
}

public sealed class PmMeetingActionRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? meetingId { get; set; }
    public string? title { get; set; }
    public string? ownerName { get; set; }
    public DateTime? dueDate { get; set; }
    public string? status { get; set; }
    public string? workItemId { get; set; }
    public string? wbsId { get; set; }
    public string? note { get; set; }
    public DateTime? closedAt { get; set; }
    public string? closedBy { get; set; }
}

public sealed class PmStakeholderRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? name { get; set; }
    public string? organization { get; set; }
    public string? kind { get; set; }
    public string? email { get; set; }
    public string? wbsId { get; set; }
    public string? status { get; set; }
    public DateTime? accessUntil { get; set; }
    public List<string>? resourceIds { get; set; }
    public string? note { get; set; }
    public DateTime? revokedAt { get; set; }
    public string? revokedBy { get; set; }
}

public sealed class PmProcessMapRow
{
    public string? __dataId { get; set; }
    public string? projectId { get; set; }
    public string? name { get; set; }
    public string? kind { get; set; }
    public string? resourceId { get; set; }
    public string? wbsId { get; set; }
    public string? status { get; set; }
    public string? note { get; set; }
    public DateTime? currentAt { get; set; }
    public string? currentBy { get; set; }
    public DateTime? supersededAt { get; set; }
    public string? supersededBy { get; set; }
}
