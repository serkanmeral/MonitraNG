using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    /// <summary>
    /// Lightweight portfolio for the project list. Avoids per-project full status packs
    /// (those were timing out at the gateway with 7+ seeded projects).
    /// </summary>
    public async Task<PortfolioDto> GetPortfolioAsync(CancellationToken ct = default)
    {
        var projects = await ListProjectsAsync(ct);
        var token = RequireToken();
        var allWbs = await LoadAllWbsAsync(token, ct);
        var wbsByProject = allWbs
            .Where(w => !string.IsNullOrWhiteSpace(w.projectId))
            .GroupBy(w => w.projectId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var items = new List<PortfolioProjectDto>(projects.Count);
        foreach (var project in projects)
        {
            wbsByProject.TryGetValue(project.Id, out var wbs);
            wbs ??= [];
            var percent = wbs.Count == 0
                ? 0
                : Math.Round(wbs.Average(w => w.percentComplete ?? 0), 1);
            var drifted = project.BaselineDrifted || wbs.Any(IsDrifted);
            var unboundLeaf = CountUnboundLeaves(wbs);
            var flags = new List<string>();
            if (drifted) flags.Add(ProjectTraceFlags.Drifted);
            if (unboundLeaf > 0) flags.Add(ProjectTraceFlags.Unbound);

            var closed = string.Equals(project.Status, PmProjectStatus.Closed, StringComparison.Ordinal);
            items.Add(new PortfolioProjectDto
            {
                Id = project.Id,
                Code = project.Code,
                Name = project.Name,
                Status = project.Status,
                PlannedStart = project.PlannedStart,
                PlannedFinish = project.PlannedFinish,
                BaselineDrifted = drifted,
                PercentComplete = percent,
                Attention = !closed && flags.Count > 0,
                Flags = flags,
                Counts = new ProjectStatusCountsDto
                {
                    Drifted = drifted ? 1 : 0,
                    UnboundLeaf = unboundLeaf
                }
            });
        }

        items.Sort((a, b) =>
        {
            var byAttention = b.Attention.CompareTo(a.Attention);
            if (byAttention != 0) return byAttention;
            return string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
        });

        return new PortfolioDto
        {
            GeneratedAt = DateTime.UtcNow,
            ProjectCount = items.Count,
            DraftCount = items.Count(p => string.Equals(p.Status, PmProjectStatus.Draft, StringComparison.Ordinal)),
            ActiveCount = items.Count(p => string.Equals(p.Status, PmProjectStatus.Active, StringComparison.Ordinal)),
            ClosedCount = items.Count(p => string.Equals(p.Status, PmProjectStatus.Closed, StringComparison.Ordinal)),
            AttentionCount = items.Count(p => p.Attention),
            Totals = SumCounts(items.Select(p => p.Counts)),
            Items = items
        };
    }

    private static int CountUnboundLeaves(IReadOnlyList<PmWbsRow> wbs)
    {
        if (wbs.Count == 0) return 0;
        var parents = wbs
            .Where(w => !string.IsNullOrWhiteSpace(w.parentId))
            .Select(w => w.parentId!)
            .ToHashSet(StringComparer.Ordinal);
        return wbs.Count(w =>
            !string.IsNullOrWhiteSpace(w.__dataId)
            && !parents.Contains(w.__dataId!)
            && string.IsNullOrWhiteSpace(w.workItemId));
    }

    private static PortfolioProjectDto ToPortfolioProject(ProjectDto project, ProjectStatusPackDto pack)
    {
        var flags = AttentionFlags(pack.Counts, project.BaselineDrifted);
        var closed = string.Equals(project.Status, PmProjectStatus.Closed, StringComparison.Ordinal);
        return new PortfolioProjectDto
        {
            Id = project.Id,
            Code = project.Code,
            Name = project.Name,
            Status = project.Status,
            PlannedStart = project.PlannedStart,
            PlannedFinish = project.PlannedFinish,
            BaselineDrifted = project.BaselineDrifted,
            PercentComplete = ProjectPercent(pack.Items),
            Attention = !closed && flags.Count > 0,
            Flags = flags,
            Counts = pack.Counts
        };
    }

    private static double ProjectPercent(IReadOnlyList<ProjectTraceRowDto> rows)
    {
        if (rows.Count == 0) return 0;
        return Math.Round(rows.Average(r => r.PercentComplete), 1);
    }

    private static List<string> AttentionFlags(ProjectStatusCountsDto c, bool drifted)
    {
        var flags = new List<string>();
        if (c.Delayed > 0) flags.Add(ProjectTraceFlags.Delayed);
        if (c.MilestoneAtRisk > 0) flags.Add(ProjectTraceFlags.MilestoneAtRisk);
        if (drifted || c.Drifted > 0) flags.Add(ProjectTraceFlags.Drifted);
        if (c.FailedGate > 0) flags.Add(ProjectTraceFlags.FailedGate);
        if (c.OpenRisk > 0) flags.Add(ProjectTraceFlags.OpenRisk);
        if (c.OverloadedResource > 0) flags.Add(ProjectTraceFlags.OverloadedResource);
        if (c.OverBudget > 0) flags.Add(ProjectTraceFlags.OverBudget);
        if (c.OverdueAck > 0) flags.Add(ProjectTraceFlags.OverdueAck);
        if (c.OverdueObligation > 0) flags.Add(ProjectTraceFlags.OverdueObligation);
        if (c.OverdueAuditPack > 0) flags.Add(ProjectTraceFlags.OverdueAuditPack);
        if (c.OverdueMeetingAction > 0) flags.Add(ProjectTraceFlags.OverdueMeetingAction);
        if (c.OverdueStakeholder > 0) flags.Add(ProjectTraceFlags.OverdueStakeholder);
        return flags;
    }

    private static ProjectStatusCountsDto SumCounts(IEnumerable<ProjectStatusCountsDto> all)
    {
        var t = new ProjectStatusCountsDto();
        foreach (var c in all)
        {
            t.Delayed += c.Delayed;
            t.MilestoneAtRisk += c.MilestoneAtRisk;
            t.Drifted += c.Drifted;
            t.UnboundLeaf += c.UnboundLeaf;
            t.OpenWork += c.OpenWork;
            t.MissingEvidence += c.MissingEvidence;
            t.MissingApproval += c.MissingApproval;
            t.OpenScopeChange += c.OpenScopeChange;
            t.OpenGate += c.OpenGate;
            t.FailedGate += c.FailedGate;
            t.OpenRisk += c.OpenRisk;
            t.OpenIssue += c.OpenIssue;
            t.OpenAssumption += c.OpenAssumption;
            t.OpenDependency += c.OpenDependency;
            t.OverloadedResource += c.OverloadedResource;
            t.OverBudget += c.OverBudget;
            t.PendingAck += c.PendingAck;
            t.OverdueAck += c.OverdueAck;
            t.OpenObligation += c.OpenObligation;
            t.OverdueObligation += c.OverdueObligation;
            t.UnboundObligation += c.UnboundObligation;
            t.OpenAuditPack += c.OpenAuditPack;
            t.IncompleteAuditPack += c.IncompleteAuditPack;
            t.OverdueAuditPack += c.OverdueAuditPack;
            t.OpenMeetingAction += c.OpenMeetingAction;
            t.OverdueMeetingAction += c.OverdueMeetingAction;
            t.UnboundMeetingAction += c.UnboundMeetingAction;
            t.OpenStakeholder += c.OpenStakeholder;
            t.IncompleteStakeholder += c.IncompleteStakeholder;
            t.OverdueStakeholder += c.OverdueStakeholder;
            t.OpenProcessMap += c.OpenProcessMap;
            t.IncompleteProcessMap += c.IncompleteProcessMap;
            t.CurrentProcessMap += c.CurrentProcessMap;
        }
        return t;
    }
}
