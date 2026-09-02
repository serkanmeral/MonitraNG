using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectPlanningService _planning;

    public ProjectsController(IProjectPlanningService planning)
    {
        _planning = planning;
    }

    [HttpGet("projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _planning.ListProjectsAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("projects/portfolio")]
    [ProducesResponseType(typeof(PortfolioDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPortfolio(CancellationToken cancellationToken)
    {
        var pack = await _planning.GetPortfolioAsync(cancellationToken);
        return Ok(pack);
    }

    [HttpGet("job-packs")]
    [ProducesResponseType(typeof(IReadOnlyList<JobPackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobPacks(CancellationToken cancellationToken)
    {
        var items = await _planning.ListJobPacksAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("projects/{id}/packs")]
    [ProducesResponseType(typeof(ProjectPackCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectPacks(string id, CancellationToken cancellationToken)
    {
        var catalog = await _planning.GetProjectPacksAsync(id, cancellationToken);
        return Ok(catalog);
    }

    [HttpGet("projects/{id}/packs/{packCode}/preview")]
    [ProducesResponseType(typeof(PackPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewPack(
        string id,
        string packCode,
        [FromQuery] string? intent,
        [FromQuery] string? mode,
        CancellationToken cancellationToken)
    {
        var preview = await _planning.PreviewPackAsync(id, packCode, intent, mode, cancellationToken);
        return Ok(preview);
    }

    [HttpPost("projects/{id}/packs/{packCode}")]
    [ProducesResponseType(typeof(ApplyPackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyPack(
        string id,
        string packCode,
        [FromQuery] string? mode,
        CancellationToken cancellationToken)
    {
        var result = await _planning.ApplyPackAsync(id, packCode, mode, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("projects/{id}/packs/{packCode}")]
    [ProducesResponseType(typeof(ApplyPackResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DetachPack(string id, string packCode, CancellationToken cancellationToken)
    {
        var result = await _planning.DetachPackAsync(id, packCode, cancellationToken);
        return Ok(result);
    }

    [HttpPost("projects")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var created = await _planning.CreateProjectAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("projects/{id}")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var detail = await _planning.GetProjectAsync(id, cancellationToken);
        return Ok(detail);
    }

    [HttpPut("projects/{id}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateProjectAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("projects/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteProjectAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id}/baseline")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetBaseline(
        string id,
        [FromBody] SetProjectBaselineRequest? request,
        CancellationToken cancellationToken)
    {
        var detail = await _planning.SetBaselineAsync(id, request ?? new SetProjectBaselineRequest(), cancellationToken);
        return Ok(detail);
    }

    [HttpPost("projects/{id}/wbs")]
    [ProducesResponseType(typeof(WbsItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateWbs(string id, [FromBody] CreateWbsItemRequest request, CancellationToken cancellationToken)
    {
        var created = await _planning.CreateWbsAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("wbs/{id}")]
    [ProducesResponseType(typeof(WbsItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWbs(string id, [FromBody] UpdateWbsItemRequest request, CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateWbsAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("wbs/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWbs(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteWbsAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id}/dependencies")]
    [ProducesResponseType(typeof(DependencyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDependency(
        string id,
        [FromBody] CreateDependencyRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateDependencyAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpDelete("dependencies/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDependency(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteDependencyAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("wbs/{id}/work-item")]
    [ProducesResponseType(typeof(WbsItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BindWorkItem(
        string id,
        [FromBody] BindWbsWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.BindWorkItemAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("wbs/{id}/work-item")]
    [ProducesResponseType(typeof(WbsItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnbindWorkItem(string id, CancellationToken cancellationToken)
    {
        var updated = await _planning.UnbindWorkItemAsync(id, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("projects/{id}/work-items")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkItemCandidateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchWorkItems(
        string id,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var items = await _planning.SearchWorkItemsAsync(id, q, cancellationToken);
        return Ok(items);
    }

    [HttpPost("projects/{id}/rollup")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecalcProgress(string id, CancellationToken cancellationToken)
    {
        var detail = await _planning.RecalcProgressAsync(id, cancellationToken);
        return Ok(detail);
    }

    [HttpGet("projects/{id}/status")]
    [ProducesResponseType(typeof(ProjectStatusPackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetStatusPackAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/decisions")]
    [ProducesResponseType(typeof(DecisionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateDecision(
        string id,
        [FromBody] CreateDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateDecisionAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("decisions/{id}")]
    [ProducesResponseType(typeof(DecisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDecision(
        string id,
        [FromBody] UpdateDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateDecisionAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("decisions/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDecision(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteDecisionAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id}/stage-gates")]
    [ProducesResponseType(typeof(StageGateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateStageGate(
        string id,
        [FromBody] CreateStageGateRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateStageGateAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("stage-gates/{id}")]
    [ProducesResponseType(typeof(StageGateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStageGate(
        string id,
        [FromBody] UpdateStageGateRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateStageGateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("stage-gates/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStageGate(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteStageGateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("projects/{id}/raid")]
    [ProducesResponseType(typeof(RaidItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateRaidItem(
        string id,
        [FromBody] CreateRaidItemRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateRaidItemAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("raid/{id}")]
    [ProducesResponseType(typeof(RaidItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRaidItem(
        string id,
        [FromBody] UpdateRaidItemRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateRaidItemAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("raid/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRaidItem(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteRaidItemAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/capacity")]
    [ProducesResponseType(typeof(ProjectCapacityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCapacity(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetCapacityAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/assignments")]
    [ProducesResponseType(typeof(ResourceAssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAssignment(
        string id,
        [FromBody] CreateResourceAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateAssignmentAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("assignments/{id}")]
    [ProducesResponseType(typeof(ResourceAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAssignment(
        string id,
        [FromBody] UpdateResourceAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateAssignmentAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("assignments/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignment(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteAssignmentAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/budget")]
    [ProducesResponseType(typeof(ProjectBudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBudget(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetBudgetAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/budget")]
    [ProducesResponseType(typeof(BudgetLineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateBudgetLine(
        string id,
        [FromBody] CreateBudgetLineRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateBudgetLineAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("budget/{id}")]
    [ProducesResponseType(typeof(BudgetLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateBudgetLine(
        string id,
        [FromBody] UpdateBudgetLineRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateBudgetLineAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("budget/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBudgetLine(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteBudgetLineAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/acks")]
    [ProducesResponseType(typeof(ProjectAcknowledgementsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAcknowledgements(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetAcknowledgementsAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/acks")]
    [ProducesResponseType(typeof(AcknowledgementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAcknowledgement(
        string id,
        [FromBody] CreateAcknowledgementRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateAcknowledgementAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("acks/{id}")]
    [ProducesResponseType(typeof(AcknowledgementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAcknowledgement(
        string id,
        [FromBody] UpdateAcknowledgementRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateAcknowledgementAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("acks/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAcknowledgement(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteAcknowledgementAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/obligations")]
    [ProducesResponseType(typeof(ProjectObligationsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetObligations(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetObligationsAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/obligations")]
    [ProducesResponseType(typeof(ObligationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateObligation(
        string id,
        [FromBody] CreateObligationRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateObligationAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("obligations/{id}")]
    [ProducesResponseType(typeof(ObligationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateObligation(
        string id,
        [FromBody] UpdateObligationRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateObligationAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("obligations/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteObligation(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteObligationAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/audit-packs")]
    [ProducesResponseType(typeof(ProjectAuditPacksDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditPacks(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetAuditPacksAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/audit-packs")]
    [ProducesResponseType(typeof(AuditPackDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAuditPack(
        string id,
        [FromBody] CreateAuditPackRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateAuditPackAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("audit-packs/{id}")]
    [ProducesResponseType(typeof(AuditPackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAuditPack(
        string id,
        [FromBody] UpdateAuditPackRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateAuditPackAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("audit-packs/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAuditPack(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteAuditPackAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/meetings")]
    [ProducesResponseType(typeof(ProjectMeetingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeetings(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetMeetingsAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/meetings")]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMeeting(
        string id,
        [FromBody] CreateMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateMeetingAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("meetings/{id}")]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMeeting(
        string id,
        [FromBody] UpdateMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateMeetingAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("meetings/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMeeting(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteMeetingAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("meetings/{id}/actions")]
    [ProducesResponseType(typeof(MeetingActionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMeetingAction(
        string id,
        [FromBody] CreateMeetingActionRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateMeetingActionAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("meeting-actions/{id}")]
    [ProducesResponseType(typeof(MeetingActionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMeetingAction(
        string id,
        [FromBody] UpdateMeetingActionRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateMeetingActionAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("meeting-actions/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMeetingAction(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteMeetingActionAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/stakeholders")]
    [ProducesResponseType(typeof(ProjectStakeholdersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStakeholders(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetStakeholdersAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/stakeholders")]
    [ProducesResponseType(typeof(StakeholderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateStakeholder(
        string id,
        [FromBody] CreateStakeholderRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateStakeholderAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("stakeholders/{id}")]
    [ProducesResponseType(typeof(StakeholderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStakeholder(
        string id,
        [FromBody] UpdateStakeholderRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateStakeholderAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("stakeholders/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStakeholder(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteStakeholderAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("projects/{id}/process-maps")]
    [ProducesResponseType(typeof(ProjectProcessMapsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProcessMaps(string id, CancellationToken cancellationToken)
    {
        var pack = await _planning.GetProcessMapsAsync(id, cancellationToken);
        return Ok(pack);
    }

    [HttpPost("projects/{id}/process-maps")]
    [ProducesResponseType(typeof(ProcessMapDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProcessMap(
        string id,
        [FromBody] CreateProcessMapRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _planning.CreateProcessMapAsync(id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("process-maps/{id}")]
    [ProducesResponseType(typeof(ProcessMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProcessMap(
        string id,
        [FromBody] UpdateProcessMapRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _planning.UpdateProcessMapAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("process-maps/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProcessMap(string id, CancellationToken cancellationToken)
    {
        await _planning.DeleteProcessMapAsync(id, cancellationToken);
        return NoContent();
    }
}
