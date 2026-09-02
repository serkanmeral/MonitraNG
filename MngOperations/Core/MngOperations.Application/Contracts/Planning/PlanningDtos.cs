namespace MngOperations.Application.Contracts.Planning;

public sealed class ProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public DateTime? BaselineSetAt { get; set; }
    public string? BaselineSetBy { get; set; }
    public string? BaselineNote { get; set; }
    public bool BaselineDrifted { get; set; }
    public string? DiFolderId { get; set; }
    public string? WorkspaceId { get; set; }
}

public sealed class WbsItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string Kind { get; set; } = "task";
    public string Name { get; set; } = string.Empty;
    public string? WbsCode { get; set; }
    public int SortOrder { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public DateTime? BaselineStart { get; set; }
    public DateTime? BaselineFinish { get; set; }
    public double Weight { get; set; } = 1;
    public double PercentComplete { get; set; }
    public string? WorkItemId { get; set; }
    public string? WorkItemKey { get; set; }
    public string? WorkItemTitle { get; set; }
    public string? WorkItemStateName { get; set; }
    public string? WorkItemStateCategory { get; set; }
    public bool WorkItemClosed { get; set; }
    public bool BaselineDrifted { get; set; }
}

public sealed class DependencyDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PredecessorId { get; set; } = string.Empty;
    public string SuccessorId { get; set; } = string.Empty;
    public string Type { get; set; } = "FS";
    public int LagDays { get; set; }
}

public sealed class DecisionDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string Kind { get; set; } = "general";
    public string Status { get; set; } = "open";
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DocumentId { get; set; }
    public string? DocumentName { get; set; }
    public IReadOnlyList<string> WbsIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> WorkItemIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ResourceIds { get; set; } = Array.Empty<string>();
}

public sealed class CreateDecisionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Kind { get; set; }
    public string? Status { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DocumentId { get; set; }
    public IReadOnlyList<string>? WbsIds { get; set; }
    public IReadOnlyList<string>? WorkItemIds { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
}

public sealed class UpdateDecisionRequest
{
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Kind { get; set; }
    public string? Status { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DocumentId { get; set; }
    public IReadOnlyList<string>? WbsIds { get; set; }
    public IReadOnlyList<string>? WorkItemIds { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
}

public sealed class ProjectDetailDto
{
    public ProjectDto Project { get; set; } = new();
    public IReadOnlyList<WbsItemDto> Wbs { get; set; } = Array.Empty<WbsItemDto>();
    public IReadOnlyList<DependencyDto> Dependencies { get; set; } = Array.Empty<DependencyDto>();
    public IReadOnlyList<DecisionDto> Decisions { get; set; } = Array.Empty<DecisionDto>();
    public IReadOnlyList<StageGateDto> StageGates { get; set; } = Array.Empty<StageGateDto>();
    public IReadOnlyList<RaidItemDto> RaidItems { get; set; } = Array.Empty<RaidItemDto>();
    public IReadOnlyList<ResourceAssignmentDto> Assignments { get; set; } = Array.Empty<ResourceAssignmentDto>();
    public ProjectCapacityDto Capacity { get; set; } = new();
    public IReadOnlyList<BudgetLineDto> BudgetLines { get; set; } = Array.Empty<BudgetLineDto>();
    public ProjectBudgetDto Budget { get; set; } = new();
    public IReadOnlyList<AcknowledgementDto> Acknowledgements { get; set; } = Array.Empty<AcknowledgementDto>();
    public IReadOnlyList<ObligationDto> Obligations { get; set; } = Array.Empty<ObligationDto>();
    public IReadOnlyList<AuditPackDto> AuditPacks { get; set; } = Array.Empty<AuditPackDto>();
    public IReadOnlyList<MeetingDto> Meetings { get; set; } = Array.Empty<MeetingDto>();
    public IReadOnlyList<StakeholderDto> Stakeholders { get; set; } = Array.Empty<StakeholderDto>();
    public IReadOnlyList<ProcessMapDto> ProcessMaps { get; set; } = Array.Empty<ProcessMapDto>();
}

public sealed class CreateProjectRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    /// <summary>Optional F1-9 job pack code (pmo | quality). Seeds WBS skeleton.</summary>
    public string? PackCode { get; set; }
}

public sealed class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public string? WorkspaceId { get; set; }
    public string? DiFolderId { get; set; }
}

public sealed class SetProjectBaselineRequest
{
    public string? Note { get; set; }
}

public sealed class CreateWbsItemRequest
{
    public string? ParentId { get; set; }
    public string? Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public double? Weight { get; set; }
    public double? PercentComplete { get; set; }
}

public sealed class UpdateWbsItemRequest
{
    public string? ParentId { get; set; }
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public int? SortOrder { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualFinish { get; set; }
    public double? Weight { get; set; }
    public double? PercentComplete { get; set; }
}

public sealed class BindWbsWorkItemRequest
{
    public string WorkItemId { get; set; } = string.Empty;
}

public sealed class WorkItemCandidateDto
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? StateName { get; set; }
    public string? StateCategory { get; set; }
    public bool Closed { get; set; }
}

public sealed class CreateDependencyRequest
{
    public string PredecessorId { get; set; } = string.Empty;
    public string SuccessorId { get; set; } = string.Empty;
    public string? Type { get; set; }
    public int? LagDays { get; set; }
}

public static class ProjectTraceFlags
{
    public const string Delayed = "delayed";
    public const string MilestoneAtRisk = "milestoneAtRisk";
    public const string Drifted = "drifted";
    public const string Unbound = "unbound";
    public const string OpenWork = "openWork";
    public const string MissingEvidence = "missingEvidence";
    public const string MissingApproval = "missingApproval";
    public const string OpenGate = "openGate";
    public const string FailedGate = "failedGate";
    public const string OpenRisk = "openRisk";
    public const string OpenIssue = "openIssue";
    public const string OverloadedResource = "overloadedResource";
    public const string OverBudget = "overBudget";
    public const string PendingAck = "pendingAck";
    public const string OverdueAck = "overdueAck";
    public const string OpenObligation = "openObligation";
    public const string OverdueObligation = "overdueObligation";
    public const string UnboundObligation = "unboundObligation";
    public const string OpenAuditPack = "openAuditPack";
    public const string IncompleteAuditPack = "incompleteAuditPack";
    public const string OverdueAuditPack = "overdueAuditPack";
    public const string OpenMeetingAction = "openMeetingAction";
    public const string OverdueMeetingAction = "overdueMeetingAction";
    public const string UnboundMeetingAction = "unboundMeetingAction";
    public const string OpenStakeholder = "openStakeholder";
    public const string IncompleteStakeholder = "incompleteStakeholder";
    public const string OverdueStakeholder = "overdueStakeholder";
    public const string OpenProcessMap = "openProcessMap";
    public const string IncompleteProcessMap = "incompleteProcessMap";
}

public sealed class TraceDocumentDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public string Status { get; set; } = "published";
    public bool Approved { get; set; }
}

public sealed class ProjectTraceRowDto
{
    public string WbsId { get; set; } = string.Empty;
    public string? WbsCode { get; set; }
    public string WbsName { get; set; } = string.Empty;
    public string Kind { get; set; } = "task";
    public double PercentComplete { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public bool BaselineDrifted { get; set; }
    public string? WorkItemId { get; set; }
    public string? WorkItemKey { get; set; }
    public string? WorkItemTitle { get; set; }
    public string? WorkItemStateName { get; set; }
    public bool WorkItemClosed { get; set; }
    public IReadOnlyList<TraceDocumentDto> Documents { get; set; } = Array.Empty<TraceDocumentDto>();
    public IReadOnlyList<string> Flags { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DecisionDto> Decisions { get; set; } = Array.Empty<DecisionDto>();
    public IReadOnlyList<RaidItemDto> RaidItems { get; set; } = Array.Empty<RaidItemDto>();
}

public sealed class ProjectStatusCountsDto
{
    public int Delayed { get; set; }
    public int MilestoneAtRisk { get; set; }
    public int Drifted { get; set; }
    public int UnboundLeaf { get; set; }
    public int OpenWork { get; set; }
    public int MissingEvidence { get; set; }
    public int MissingApproval { get; set; }
    public int OpenScopeChange { get; set; }
    public int OpenGate { get; set; }
    public int FailedGate { get; set; }
    public int OpenRisk { get; set; }
    public int OpenIssue { get; set; }
    public int OpenAssumption { get; set; }
    public int OpenDependency { get; set; }
    public int OverloadedResource { get; set; }
    public int OverBudget { get; set; }
    public int PendingAck { get; set; }
    public int OverdueAck { get; set; }
    public int OpenObligation { get; set; }
    public int OverdueObligation { get; set; }
    public int UnboundObligation { get; set; }
    public int OpenAuditPack { get; set; }
    public int IncompleteAuditPack { get; set; }
    public int OverdueAuditPack { get; set; }
    public int OpenMeetingAction { get; set; }
    public int OverdueMeetingAction { get; set; }
    public int UnboundMeetingAction { get; set; }
    public int OpenStakeholder { get; set; }
    public int IncompleteStakeholder { get; set; }
    public int OverdueStakeholder { get; set; }
    public int OpenProcessMap { get; set; }
    public int IncompleteProcessMap { get; set; }
    public int CurrentProcessMap { get; set; }
}

public sealed class ProjectStatusPackDto
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public ProjectStatusCountsDto Counts { get; set; } = new();
    public IReadOnlyList<ProjectTraceRowDto> Items { get; set; } = Array.Empty<ProjectTraceRowDto>();
    public IReadOnlyList<StageGateDto> Gates { get; set; } = Array.Empty<StageGateDto>();
    public IReadOnlyList<RaidItemDto> RaidItems { get; set; } = Array.Empty<RaidItemDto>();
    public ProjectCapacityDto Capacity { get; set; } = new();
    public ProjectBudgetDto Budget { get; set; } = new();
    public ProjectAcknowledgementsDto Acknowledgements { get; set; } = new();
    public ProjectObligationsDto Obligations { get; set; } = new();
    public ProjectAuditPacksDto AuditPacks { get; set; } = new();
    public ProjectMeetingActionsDto MeetingActions { get; set; } = new();
    public ProjectStakeholdersDto Stakeholders { get; set; } = new();
    public ProjectProcessMapsDto ProcessMaps { get; set; } = new();
}

public sealed class PortfolioProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "draft";
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedFinish { get; set; }
    public bool BaselineDrifted { get; set; }
    public double PercentComplete { get; set; }
    public bool Attention { get; set; }
    public IReadOnlyList<string> Flags { get; set; } = Array.Empty<string>();
    public ProjectStatusCountsDto Counts { get; set; } = new();
}

public sealed class PortfolioDto
{
    public DateTime GeneratedAt { get; set; }
    public int ProjectCount { get; set; }
    public int DraftCount { get; set; }
    public int ActiveCount { get; set; }
    public int ClosedCount { get; set; }
    public int AttentionCount { get; set; }
    public ProjectStatusCountsDto Totals { get; set; } = new();
    public IReadOnlyList<PortfolioProjectDto> Items { get; set; } = Array.Empty<PortfolioProjectDto>();
}

public sealed class JobPackDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? Description { get; set; }
    public IReadOnlyList<string> Kinds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Folders { get; set; } = Array.Empty<string>();
    public IReadOnlyList<JobPackWbsPreview> Wbs { get; set; } = Array.Empty<JobPackWbsPreview>();
    public IReadOnlyList<JobPackStarterDto> Starters { get; set; } = Array.Empty<JobPackStarterDto>();
}

public sealed class JobPackStarterDto
{
    public string Folder { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? Body { get; set; }
}

public sealed class JobPackWbsPreview
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "task";
    public IReadOnlyList<JobPackWbsPreview> Children { get; set; } = Array.Empty<JobPackWbsPreview>();
}

public sealed class ProjectPackInstallDto
{
    public string PackCode { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime? AppliedAt { get; set; }
    public string? AppliedBy { get; set; }
    public bool Outdated { get; set; }
}

public sealed class ProjectPackCatalogDto
{
    public IReadOnlyList<JobPackDto> Catalog { get; set; } = Array.Empty<JobPackDto>();
    public IReadOnlyList<ProjectPackInstallDto> Installed { get; set; } = Array.Empty<ProjectPackInstallDto>();
}

public sealed class ApplyPackResultDto
{
    public string PackCode { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Updated { get; set; }
    public int Removed { get; set; }
    public int Kept { get; set; }
    public bool WorkspaceCreated { get; set; }
    public string? WorkspaceId { get; set; }
}

public sealed class PackPreviewItemDto
{
    public string Path { get; set; } = string.Empty;
    public string Kind { get; set; } = "task";
    public string Action { get; set; } = "skip";
    public string? WbsId { get; set; }
}

public sealed class PackPreviewDto
{
    public string PackCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? InstalledVersion { get; set; }
    public bool Outdated { get; set; }
    public string Intent { get; set; } = "apply";
    public int CreateCount { get; set; }
    public int SkipCount { get; set; }
    public int UpdateCount { get; set; }
    public int RemoveCount { get; set; }
    public int KeepCount { get; set; }
    public IReadOnlyList<PackPreviewItemDto> Items { get; set; } = Array.Empty<PackPreviewItemDto>();
    public string WorkspaceAction { get; set; } = "skip";
    public string? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }
}

public sealed class StageGateDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? WbsId { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = "open";
    public IReadOnlyList<string> Criteria { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Satisfied { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public IReadOnlyList<string> ResourceIds { get; set; } = Array.Empty<string>();
    public string? DecisionId { get; set; }
}

public sealed class CreateStageGateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? WbsId { get; set; }
    public int? SortOrder { get; set; }
    public string? Status { get; set; }
    public IReadOnlyList<string>? Criteria { get; set; }
    public IReadOnlyList<string>? Satisfied { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
    public string? DecisionId { get; set; }
}

public sealed class UpdateStageGateRequest
{
    public string? Name { get; set; }
    public string? WbsId { get; set; }
    public int? SortOrder { get; set; }
    public string? Status { get; set; }
    public IReadOnlyList<string>? Criteria { get; set; }
    public IReadOnlyList<string>? Satisfied { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
    public string? DecisionId { get; set; }
}

public sealed class RaidItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Kind { get; set; } = "risk";
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string Status { get; set; } = "open";
    public string Impact { get; set; } = "medium";
    public string Likelihood { get; set; } = "medium";
    public string Response { get; set; } = "none";
    public string? Owner { get; set; }
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<string> WbsIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> WorkItemIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ResourceIds { get; set; } = Array.Empty<string>();
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public int Score { get; set; }
    public bool Elevated { get; set; }
    public bool Open { get; set; }
}

public sealed class CreateRaidItemRequest
{
    public string Kind { get; set; } = "risk";
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Status { get; set; }
    public string? Impact { get; set; }
    public string? Likelihood { get; set; }
    public string? Response { get; set; }
    public string? Owner { get; set; }
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<string>? WbsIds { get; set; }
    public IReadOnlyList<string>? WorkItemIds { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
}

public sealed class UpdateRaidItemRequest
{
    public string? Kind { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? Status { get; set; }
    public string? Impact { get; set; }
    public string? Likelihood { get; set; }
    public string? Response { get; set; }
    public string? Owner { get; set; }
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<string>? WbsIds { get; set; }
    public IReadOnlyList<string>? WorkItemIds { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
}

public sealed class ResourceAssignmentDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string WbsId { get; set; } = string.Empty;
    public string? PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public double PlannedHours { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? Finish { get; set; }
    public DateTime? EffectiveStart { get; set; }
    public DateTime? EffectiveFinish { get; set; }
    public bool Unscheduled { get; set; }
}

public sealed class CreateResourceAssignmentRequest
{
    public string WbsId { get; set; } = string.Empty;
    public string? PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public double PlannedHours { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? Finish { get; set; }
}

public sealed class UpdateResourceAssignmentRequest
{
    public string? WbsId { get; set; }
    public string? PersonId { get; set; }
    public string? Name { get; set; }
    public string? Role { get; set; }
    public double? PlannedHours { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? Finish { get; set; }
}

public sealed class CapacityWeekDto
{
    public DateTime WeekStart { get; set; }
    public double Hours { get; set; }
    public double CapacityHours { get; set; }
    public bool Overloaded { get; set; }
}

public sealed class CapacityPersonDto
{
    public string Key { get; set; } = string.Empty;
    public string? PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double TotalHours { get; set; }
    public double UnscheduledHours { get; set; }
    public double WeeklyCapacityHours { get; set; } = 40;
    public bool Overloaded { get; set; }
    public IReadOnlyList<CapacityWeekDto> Weeks { get; set; } = Array.Empty<CapacityWeekDto>();
}

public sealed class ProjectCapacityDto
{
    public double WeeklyCapacityHours { get; set; } = 40;
    public int OverloadedCount { get; set; }
    public IReadOnlyList<ResourceAssignmentDto> Assignments { get; set; } = Array.Empty<ResourceAssignmentDto>();
    public IReadOnlyList<CapacityPersonDto> People { get; set; } = Array.Empty<CapacityPersonDto>();
}

public sealed class BudgetLineDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string WbsId { get; set; } = string.Empty;
    public string Category { get; set; } = "labor";
    public string Name { get; set; } = string.Empty;
    public double PlannedAmount { get; set; }
    public double ActualAmount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Note { get; set; }
    public double Variance { get; set; }
    public bool Over { get; set; }
}

public sealed class CreateBudgetLineRequest
{
    public string WbsId { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public double PlannedAmount { get; set; }
    public double ActualAmount { get; set; }
    public string? Currency { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateBudgetLineRequest
{
    public string? WbsId { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public double? PlannedAmount { get; set; }
    public double? ActualAmount { get; set; }
    public string? Currency { get; set; }
    public string? Note { get; set; }
}

public sealed class BudgetPackageDto
{
    public string WbsId { get; set; } = string.Empty;
    public double PlannedAmount { get; set; }
    public double ActualAmount { get; set; }
    public double Variance { get; set; }
    public bool Over { get; set; }
    public string Currency { get; set; } = "TRY";
}

public sealed class ProjectBudgetDto
{
    public string Currency { get; set; } = "TRY";
    public double PlannedAmount { get; set; }
    public double ActualAmount { get; set; }
    public double Variance { get; set; }
    public int OverCount { get; set; }
    public IReadOnlyList<BudgetLineDto> Lines { get; set; } = Array.Empty<BudgetLineDto>();
    public IReadOnlyList<BudgetPackageDto> Packages { get; set; } = Array.Empty<BudgetPackageDto>();
}

public sealed class AcknowledgementDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? VersionLabel { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonId { get; set; }
    public string? WbsId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public bool Pending { get; set; }
    public bool Overdue { get; set; }
}

public sealed class CreateAcknowledgementRequest
{
    public string ResourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? VersionLabel { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonId { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateAcknowledgementRequest
{
    public string? ResourceId { get; set; }
    public string? Title { get; set; }
    public string? VersionLabel { get; set; }
    public string? PersonName { get; set; }
    public string? PersonId { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectAcknowledgementsDto
{
    public int PendingCount { get; set; }
    public int OverdueCount { get; set; }
    public IReadOnlyList<AcknowledgementDto> Items { get; set; } = Array.Empty<AcknowledgementDto>();
}

public sealed class ObligationDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ClauseRef { get; set; }
    public string? SourceResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? WorkItemId { get; set; }
    public string? EvidenceResourceId { get; set; }
    public string Status { get; set; } = "open";
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public bool Open { get; set; }
    public bool Overdue { get; set; }
    public bool Unbound { get; set; }
    public bool MissingEvidence { get; set; }
}

public sealed class CreateObligationRequest
{
    public string Title { get; set; } = string.Empty;
    public string? ClauseRef { get; set; }
    public string? SourceResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? WorkItemId { get; set; }
    public string? EvidenceResourceId { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateObligationRequest
{
    public string? Title { get; set; }
    public string? ClauseRef { get; set; }
    public string? SourceResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? WorkItemId { get; set; }
    public string? EvidenceResourceId { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectObligationsDto
{
    public int OpenCount { get; set; }
    public int OverdueCount { get; set; }
    public int UnboundCount { get; set; }
    public IReadOnlyList<ObligationDto> Items { get; set; } = Array.Empty<ObligationDto>();
}

public sealed class AuditPackDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "audit";
    public string? WbsId { get; set; }
    public string Status { get; set; } = "draft";
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<string> ResourceIds { get; set; } = Array.Empty<string>();
    public string? Recipient { get; set; }
    public string? Note { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string? IssuedBy { get; set; }
    public int ItemCount { get; set; }
    public bool Open { get; set; }
    public bool Incomplete { get; set; }
    public bool Overdue { get; set; }
}

public sealed class CreateAuditPackRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
    public string? Recipient { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateAuditPackRequest
{
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public DateTime? DueDate { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
    public string? Recipient { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectAuditPacksDto
{
    public int OpenCount { get; set; }
    public int IncompleteCount { get; set; }
    public int OverdueCount { get; set; }
    public IReadOnlyList<AuditPackDto> Items { get; set; } = Array.Empty<AuditPackDto>();
}

public sealed class MeetingDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? HeldAt { get; set; }
    public string? MinutesResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? Attendees { get; set; }
    public string? Note { get; set; }
    public int ActionCount { get; set; }
    public int OpenActionCount { get; set; }
    public IReadOnlyList<MeetingActionDto> Actions { get; set; } = Array.Empty<MeetingActionDto>();
}

public sealed class CreateMeetingRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime? HeldAt { get; set; }
    public string? MinutesResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? Attendees { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateMeetingRequest
{
    public string? Name { get; set; }
    public DateTime? HeldAt { get; set; }
    public string? MinutesResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? Attendees { get; set; }
    public string? Note { get; set; }
}

public sealed class MeetingActionDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string MeetingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "open";
    public string? WorkItemId { get; set; }
    public string? WbsId { get; set; }
    public string? Note { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public bool Open { get; set; }
    public bool Overdue { get; set; }
    public bool Unbound { get; set; }
}

public sealed class CreateMeetingActionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public string? WorkItemId { get; set; }
    public string? WbsId { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateMeetingActionRequest
{
    public string? Title { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public string? WorkItemId { get; set; }
    public string? WbsId { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectMeetingsDto
{
    public int OpenActionCount { get; set; }
    public int OverdueActionCount { get; set; }
    public int UnboundActionCount { get; set; }
    public IReadOnlyList<MeetingDto> Items { get; set; } = Array.Empty<MeetingDto>();
}

public sealed class ProjectMeetingActionsDto
{
    public int OpenCount { get; set; }
    public int OverdueCount { get; set; }
    public int UnboundCount { get; set; }
    public IReadOnlyList<MeetingActionDto> Items { get; set; } = Array.Empty<MeetingActionDto>();
}

public sealed class StakeholderDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string Kind { get; set; } = "customer";
    public string? Email { get; set; }
    public string? WbsId { get; set; }
    public string Status { get; set; } = "invited";
    public DateTime? AccessUntil { get; set; }
    public IReadOnlyList<string> ResourceIds { get; set; } = Array.Empty<string>();
    public string? Note { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public int ItemCount { get; set; }
    public bool Open { get; set; }
    public bool Incomplete { get; set; }
    public bool Overdue { get; set; }
}

public sealed class CreateStakeholderRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string? Kind { get; set; }
    public string? Email { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public DateTime? AccessUntil { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateStakeholderRequest
{
    public string? Name { get; set; }
    public string? Organization { get; set; }
    public string? Kind { get; set; }
    public string? Email { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public DateTime? AccessUntil { get; set; }
    public IReadOnlyList<string>? ResourceIds { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectStakeholdersDto
{
    public int OpenCount { get; set; }
    public int IncompleteCount { get; set; }
    public int OverdueCount { get; set; }
    public IReadOnlyList<StakeholderDto> Items { get; set; } = Array.Empty<StakeholderDto>();
}

public sealed class ProcessMapDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "procedure";
    public string? ResourceId { get; set; }
    public string? WbsId { get; set; }
    public string Status { get; set; } = "draft";
    public string? Note { get; set; }
    public DateTime? CurrentAt { get; set; }
    public string? CurrentBy { get; set; }
    public DateTime? SupersededAt { get; set; }
    public string? SupersededBy { get; set; }
    public bool Open { get; set; }
    public bool Incomplete { get; set; }
    public bool Current { get; set; }
}

public sealed class CreateProcessMapRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? ResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
}

public sealed class UpdateProcessMapRequest
{
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public string? ResourceId { get; set; }
    public string? WbsId { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
}

public sealed class ProjectProcessMapsDto
{
    public int OpenCount { get; set; }
    public int IncompleteCount { get; set; }
    public int CurrentCount { get; set; }
    public IReadOnlyList<ProcessMapDto> Items { get; set; } = Array.Empty<ProcessMapDto>();
}
