using MngOperations.Application.Contracts.Planning;

namespace MngOperations.Application.Interfaces;

public interface IProjectPlanningService
{
    Task<IReadOnlyList<ProjectDto>> ListProjectsAsync(CancellationToken ct = default);

    Task<PortfolioDto> GetPortfolioAsync(CancellationToken ct = default);

    Task<IReadOnlyList<JobPackDto>> ListJobPacksAsync(CancellationToken ct = default);

    Task<ProjectPackCatalogDto> GetProjectPacksAsync(string projectId, CancellationToken ct = default);

    Task<PackPreviewDto> PreviewPackAsync(string projectId, string packCode, string? intent = null, string? mode = null, CancellationToken ct = default);

    Task<ApplyPackResultDto> ApplyPackAsync(string projectId, string packCode, string? mode = null, CancellationToken ct = default);

    Task<ApplyPackResultDto> DetachPackAsync(string projectId, string packCode, CancellationToken ct = default);

    Task<ProjectDetailDto> GetProjectAsync(string id, CancellationToken ct = default);

    Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);

    Task<ProjectDto> UpdateProjectAsync(string id, UpdateProjectRequest request, CancellationToken ct = default);

    Task DeleteProjectAsync(string id, CancellationToken ct = default);

    Task<ProjectDetailDto> SetBaselineAsync(string id, SetProjectBaselineRequest request, CancellationToken ct = default);

    Task<WbsItemDto> CreateWbsAsync(string projectId, CreateWbsItemRequest request, CancellationToken ct = default);

    Task<WbsItemDto> UpdateWbsAsync(string id, UpdateWbsItemRequest request, CancellationToken ct = default);

    Task DeleteWbsAsync(string id, CancellationToken ct = default);

    Task<DependencyDto> CreateDependencyAsync(string projectId, CreateDependencyRequest request, CancellationToken ct = default);

    Task DeleteDependencyAsync(string id, CancellationToken ct = default);

    Task<WbsItemDto> BindWorkItemAsync(string wbsId, BindWbsWorkItemRequest request, CancellationToken ct = default);

    Task<WbsItemDto> UnbindWorkItemAsync(string wbsId, CancellationToken ct = default);

    Task<IReadOnlyList<WorkItemCandidateDto>> SearchWorkItemsAsync(
        string projectId,
        string? query,
        CancellationToken ct = default);

    Task<ProjectDetailDto> RecalcProgressAsync(string projectId, CancellationToken ct = default);

    Task ApplyWorkItemProgressAsync(string workItemId, CancellationToken ct = default);

    Task ClearWorkItemLinksAsync(string workItemId, CancellationToken ct = default);

    Task<ProjectStatusPackDto> GetStatusPackAsync(string projectId, CancellationToken ct = default);

    Task<DecisionDto> CreateDecisionAsync(string projectId, CreateDecisionRequest request, CancellationToken ct = default);

    Task<DecisionDto> UpdateDecisionAsync(string id, UpdateDecisionRequest request, CancellationToken ct = default);

    Task DeleteDecisionAsync(string id, CancellationToken ct = default);

    Task<StageGateDto> CreateStageGateAsync(string projectId, CreateStageGateRequest request, CancellationToken ct = default);

    Task<StageGateDto> UpdateStageGateAsync(string id, UpdateStageGateRequest request, CancellationToken ct = default);

    Task DeleteStageGateAsync(string id, CancellationToken ct = default);

    Task<RaidItemDto> CreateRaidItemAsync(string projectId, CreateRaidItemRequest request, CancellationToken ct = default);

    Task<RaidItemDto> UpdateRaidItemAsync(string id, UpdateRaidItemRequest request, CancellationToken ct = default);

    Task DeleteRaidItemAsync(string id, CancellationToken ct = default);

    Task<ProjectCapacityDto> GetCapacityAsync(string projectId, CancellationToken ct = default);

    Task<ResourceAssignmentDto> CreateAssignmentAsync(string projectId, CreateResourceAssignmentRequest request, CancellationToken ct = default);

    Task<ResourceAssignmentDto> UpdateAssignmentAsync(string id, UpdateResourceAssignmentRequest request, CancellationToken ct = default);

    Task DeleteAssignmentAsync(string id, CancellationToken ct = default);

    Task<ProjectBudgetDto> GetBudgetAsync(string projectId, CancellationToken ct = default);

    Task<BudgetLineDto> CreateBudgetLineAsync(string projectId, CreateBudgetLineRequest request, CancellationToken ct = default);

    Task<BudgetLineDto> UpdateBudgetLineAsync(string id, UpdateBudgetLineRequest request, CancellationToken ct = default);

    Task DeleteBudgetLineAsync(string id, CancellationToken ct = default);

    Task<ProjectAcknowledgementsDto> GetAcknowledgementsAsync(string projectId, CancellationToken ct = default);

    Task<AcknowledgementDto> CreateAcknowledgementAsync(string projectId, CreateAcknowledgementRequest request, CancellationToken ct = default);

    Task<AcknowledgementDto> UpdateAcknowledgementAsync(string id, UpdateAcknowledgementRequest request, CancellationToken ct = default);

    Task DeleteAcknowledgementAsync(string id, CancellationToken ct = default);

    Task<ProjectObligationsDto> GetObligationsAsync(string projectId, CancellationToken ct = default);

    Task<ObligationDto> CreateObligationAsync(string projectId, CreateObligationRequest request, CancellationToken ct = default);

    Task<ObligationDto> UpdateObligationAsync(string id, UpdateObligationRequest request, CancellationToken ct = default);

    Task DeleteObligationAsync(string id, CancellationToken ct = default);

    Task<ProjectAuditPacksDto> GetAuditPacksAsync(string projectId, CancellationToken ct = default);

    Task<AuditPackDto> CreateAuditPackAsync(string projectId, CreateAuditPackRequest request, CancellationToken ct = default);

    Task<AuditPackDto> UpdateAuditPackAsync(string id, UpdateAuditPackRequest request, CancellationToken ct = default);

    Task DeleteAuditPackAsync(string id, CancellationToken ct = default);

    Task<ProjectMeetingsDto> GetMeetingsAsync(string projectId, CancellationToken ct = default);

    Task<MeetingDto> CreateMeetingAsync(string projectId, CreateMeetingRequest request, CancellationToken ct = default);

    Task<MeetingDto> UpdateMeetingAsync(string id, UpdateMeetingRequest request, CancellationToken ct = default);

    Task DeleteMeetingAsync(string id, CancellationToken ct = default);

    Task<MeetingActionDto> CreateMeetingActionAsync(string meetingId, CreateMeetingActionRequest request, CancellationToken ct = default);

    Task<MeetingActionDto> UpdateMeetingActionAsync(string id, UpdateMeetingActionRequest request, CancellationToken ct = default);

    Task DeleteMeetingActionAsync(string id, CancellationToken ct = default);

    Task<ProjectStakeholdersDto> GetStakeholdersAsync(string projectId, CancellationToken ct = default);

    Task<StakeholderDto> CreateStakeholderAsync(string projectId, CreateStakeholderRequest request, CancellationToken ct = default);

    Task<StakeholderDto> UpdateStakeholderAsync(string id, UpdateStakeholderRequest request, CancellationToken ct = default);

    Task DeleteStakeholderAsync(string id, CancellationToken ct = default);

    Task<ProjectProcessMapsDto> GetProcessMapsAsync(string projectId, CancellationToken ct = default);

    Task<ProcessMapDto> CreateProcessMapAsync(string projectId, CreateProcessMapRequest request, CancellationToken ct = default);

    Task<ProcessMapDto> UpdateProcessMapAsync(string id, UpdateProcessMapRequest request, CancellationToken ct = default);

    Task DeleteProcessMapAsync(string id, CancellationToken ct = default);
}
