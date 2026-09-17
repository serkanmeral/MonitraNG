using MngOperations.Application.Contracts.Planning;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const int PulseActionLimit = 8;
    private const int PulseMilestoneDays = 28;
    private const int PulseDecisionLimit = 5;

    public async Task<ProjectPulseDto> GetProjectPulseAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var coreTask = LoadProjectCoreAsync(projectId, token, ct, hydrate: false);
        var extrasTask = LoadProjectExtrasAsync(projectId, Array.Empty<WbsItemDto>(), token, ct);
        var packsTask = LoadProjectPackRowsAsync(projectId, token, ct);
        await Task.WhenAll(coreTask, extrasTask, packsTask);

        var core = await coreTask;
        var extras = await extrasTask;
        ApplyAssignmentSchedule(extras.Assignments, core.Wbs);
        var detail = ToDetailDto(core, extras);
        var today = DateTime.UtcNow.Date;
        var childIds = detail.Wbs
            .Where(w => !string.IsNullOrWhiteSpace(w.ParentId))
            .Select(w => w.ParentId!)
            .ToHashSet(StringComparer.Ordinal);

        var gates = detail.StageGates;
        var raid = detail.RaidItems;
        var decisions = detail.Decisions;
        var capacity = detail.Capacity;
        var budget = detail.Budget;
        var acknowledgements = BuildAcknowledgements(detail.Acknowledgements);
        var obligations = BuildObligations(detail.Obligations);
        var auditPacks = BuildAuditPacks(detail.AuditPacks);
        var meetingActions = BuildMeetingActions(detail.Meetings);
        var packRows = (await packsTask).Select(ToInstallDto).ToList();

        var delayedLeaves = new List<WbsItemDto>();
        var riskMilestones = new List<WbsItemDto>();
        var unboundLeaves = 0;
        var openWork = 0;
        var drifted = 0;
        var wbsChart = new ProjectPulseWbsChartDto();

        foreach (var wbs in detail.Wbs)
        {
            var incomplete = wbs.PercentComplete < 99.5 && !wbs.WorkItemClosed;
            var finish = wbs.PlannedFinish?.ToUniversalTime().Date;
            var isLeaf = !childIds.Contains(wbs.Id);

            if (wbs.BaselineDrifted) drifted++;
            if (isLeaf && string.IsNullOrWhiteSpace(wbs.WorkItemId)) unboundLeaves++;
            if (!string.IsNullOrWhiteSpace(wbs.WorkItemId) && incomplete) openWork++;

            var delayed = incomplete && finish is not null && finish.Value < today && isLeaf;
            if (delayed) delayedLeaves.Add(wbs);

            if (string.Equals(wbs.Kind, PmWbsKind.Milestone, StringComparison.OrdinalIgnoreCase)
                && incomplete
                && finish is not null
                && finish.Value <= today.AddDays(MilestoneRiskDays))
            {
                riskMilestones.Add(wbs);
            }

            if (!isLeaf) continue;
            if (!incomplete)
                wbsChart.Done++;
            else if (delayed)
                wbsChart.Delayed++;
            else if (string.IsNullOrWhiteSpace(wbs.WorkItemId))
                wbsChart.Unbound++;
            else if (wbs.PercentComplete > 0)
                wbsChart.InProgress++;
            else
                wbsChart.NotStarted++;
        }

        var openScope = decisions.Count(d =>
            string.Equals(d.Kind, PmDecisionKind.ScopeChange, StringComparison.Ordinal)
            && string.Equals(d.Status, PmDecisionStatus.Open, StringComparison.Ordinal));
        var openGates = gates.Count(g => string.Equals(g.Status, PmStageGateStatus.Open, StringComparison.Ordinal));
        var failedGates = gates.Count(g => string.Equals(g.Status, PmStageGateStatus.Failed, StringComparison.Ordinal));
        var openRisk = raid.Count(r => r.Kind == PmRaidKind.Risk && r.Open && r.Elevated);
        var openIssue = raid.Count(r => r.Kind == PmRaidKind.Issue && r.Open);

        var counts = new ProjectStatusCountsDto
        {
            Delayed = delayedLeaves.Count,
            MilestoneAtRisk = riskMilestones.Count,
            Drifted = drifted,
            UnboundLeaf = unboundLeaves,
            OpenWork = openWork,
            OpenScopeChange = openScope,
            OpenGate = openGates,
            FailedGate = failedGates,
            OpenRisk = openRisk,
            OpenIssue = openIssue,
            OverloadedResource = capacity.OverloadedCount,
            OverBudget = budget.OverCount,
            PendingAck = acknowledgements.PendingCount,
            OverdueAck = acknowledgements.OverdueCount,
            OpenObligation = obligations.OpenCount,
            OverdueObligation = obligations.OverdueCount,
            UnboundObligation = obligations.UnboundCount,
            OpenAuditPack = auditPacks.OpenCount,
            IncompleteAuditPack = auditPacks.IncompleteCount,
            OverdueAuditPack = auditPacks.OverdueCount,
            OpenMeetingAction = meetingActions.OpenCount,
            OverdueMeetingAction = meetingActions.OverdueCount,
            UnboundMeetingAction = meetingActions.UnboundCount
        };

        var packs = new ProjectPulsePacksDto
        {
            Installed = packRows.Count,
            Outdated = packRows.Count(p => p.Outdated)
        };

        var project = detail.Project;
        var percent = detail.Wbs.Count == 0
            ? 0
            : Math.Round(detail.Wbs.Average(w => w.PercentComplete), 1);

        return new ProjectPulseDto
        {
            ProjectId = projectId,
            GeneratedAt = DateTime.UtcNow,
            Health = ResolvePulseHealth(project.Status, counts, project.BaselineDrifted),
            Status = project.Status,
            PercentComplete = percent,
            PlannedStart = project.PlannedStart,
            PlannedFinish = project.PlannedFinish,
            BaselineDrifted = project.BaselineDrifted,
            NextGate = gates
                .Where(g => string.Equals(g.Status, PmStageGateStatus.Open, StringComparison.Ordinal))
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(),
            Counts = counts,
            Actions = BuildPulseActions(
                delayedLeaves,
                riskMilestones,
                gates,
                raid,
                obligations.Items,
                acknowledgements.Items,
                meetingActions.Items),
            UpcomingMilestones = BuildUpcomingMilestones(detail.Wbs, today),
            OpenDecisions = decisions
                .Where(d => string.Equals(d.Status, PmDecisionStatus.Open, StringComparison.Ordinal))
                .Take(PulseDecisionLimit)
                .ToList(),
            Packs = packs,
            DaysToFinish = project.PlannedFinish is null
                ? null
                : (int)Math.Ceiling((project.PlannedFinish.Value.ToUniversalTime().Date - today).TotalDays),
            Charts = new ProjectPulseChartsDto
            {
                Wbs = wbsChart,
                Gates =
                [
                    new ProjectPulseBucketDto { Key = PmStageGateStatus.Open, Count = openGates },
                    new ProjectPulseBucketDto
                    {
                        Key = PmStageGateStatus.Passed,
                        Count = gates.Count(g => string.Equals(g.Status, PmStageGateStatus.Passed, StringComparison.Ordinal))
                    },
                    new ProjectPulseBucketDto { Key = PmStageGateStatus.Failed, Count = failedGates },
                    new ProjectPulseBucketDto
                    {
                        Key = PmStageGateStatus.Waived,
                        Count = gates.Count(g => string.Equals(g.Status, PmStageGateStatus.Waived, StringComparison.Ordinal))
                    }
                ],
                Raid =
                [
                    new ProjectPulseBucketDto { Key = PmRaidKind.Risk, Count = raid.Count(r => r.Open && r.Kind == PmRaidKind.Risk) },
                    new ProjectPulseBucketDto { Key = PmRaidKind.Issue, Count = raid.Count(r => r.Open && r.Kind == PmRaidKind.Issue) },
                    new ProjectPulseBucketDto { Key = PmRaidKind.Assumption, Count = raid.Count(r => r.Open && r.Kind == PmRaidKind.Assumption) },
                    new ProjectPulseBucketDto { Key = PmRaidKind.Dependency, Count = raid.Count(r => r.Open && r.Kind == PmRaidKind.Dependency) }
                ],
                Budget = new ProjectPulseBudgetChartDto
                {
                    Planned = budget.PlannedAmount,
                    Actual = budget.ActualAmount,
                    Currency = string.IsNullOrWhiteSpace(budget.Currency) ? "TRY" : budget.Currency
                }
            }
        };
    }

    private static string ResolvePulseHealth(string status, ProjectStatusCountsDto counts, bool drifted)
    {
        if (string.Equals(status, PmProjectStatus.Closed, StringComparison.Ordinal))
            return "ok";

        if (counts.Delayed > 0
            || counts.FailedGate > 0
            || counts.OverdueObligation > 0
            || counts.OverdueAck > 0
            || counts.OverdueAuditPack > 0
            || counts.OverdueMeetingAction > 0
            || counts.OverBudget > 0
            || counts.OpenRisk > 0)
        {
            return "alert";
        }

        if (drifted
            || counts.MilestoneAtRisk > 0
            || counts.OpenGate > 0
            || counts.OpenIssue > 0
            || counts.PendingAck > 0
            || counts.OpenObligation > 0
            || counts.OpenScopeChange > 0
            || counts.OverloadedResource > 0)
        {
            return "watch";
        }

        return "ok";
    }

    private static IReadOnlyList<ProjectPulseActionDto> BuildPulseActions(
        IReadOnlyList<WbsItemDto> delayedLeaves,
        IReadOnlyList<WbsItemDto> riskMilestones,
        IReadOnlyList<StageGateDto> gates,
        IReadOnlyList<RaidItemDto> raid,
        IReadOnlyList<ObligationDto> obligations,
        IReadOnlyList<AcknowledgementDto> acks,
        IReadOnlyList<MeetingActionDto> meetingActions)
    {
        var actions = new List<ProjectPulseActionDto>();

        foreach (var gate in gates.Where(g => string.Equals(g.Status, PmStageGateStatus.Failed, StringComparison.Ordinal)))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "gates",
                Flag = ProjectTraceFlags.FailedGate,
                Title = gate.Name,
                Severity = "alert"
            });
        }

        foreach (var row in obligations.Where(o => o.Overdue).OrderBy(o => o.DueDate))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "obligations",
                Flag = ProjectTraceFlags.OverdueObligation,
                Title = row.Title,
                Due = row.DueDate,
                WbsId = row.WbsId,
                Severity = "alert"
            });
        }

        foreach (var wbs in delayedLeaves.OrderBy(w => w.PlannedFinish))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "status",
                Flag = ProjectTraceFlags.Delayed,
                Title = wbs.Name,
                Detail = wbs.WbsCode,
                WbsId = wbs.Id,
                Due = wbs.PlannedFinish,
                Severity = "alert"
            });
        }

        foreach (var row in acks.Where(a => a.Overdue).OrderBy(a => a.DueDate))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "acks",
                Flag = ProjectTraceFlags.OverdueAck,
                Title = string.IsNullOrWhiteSpace(row.PersonName) ? row.Title : $"{row.PersonName} · {row.Title}",
                Due = row.DueDate,
                WbsId = row.WbsId,
                Severity = "alert"
            });
        }

        foreach (var item in raid.Where(r => r.Kind == PmRaidKind.Risk && r.Open && r.Elevated))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "raid",
                Flag = ProjectTraceFlags.OpenRisk,
                Title = item.Title,
                Due = item.DueDate,
                Severity = "alert"
            });
        }

        foreach (var item in raid.Where(r => r.Kind == PmRaidKind.Issue && r.Open))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "raid",
                Flag = ProjectTraceFlags.OpenIssue,
                Title = item.Title,
                Due = item.DueDate,
                Severity = "watch"
            });
        }

        foreach (var wbs in riskMilestones.OrderBy(w => w.PlannedFinish))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "gantt",
                Flag = ProjectTraceFlags.MilestoneAtRisk,
                Title = wbs.Name,
                Detail = wbs.WbsCode,
                WbsId = wbs.Id,
                Due = wbs.PlannedFinish,
                Severity = "watch"
            });
        }

        foreach (var row in meetingActions.Where(a => a.Overdue).OrderBy(a => a.DueDate))
        {
            actions.Add(new ProjectPulseActionDto
            {
                Tab = "meetings",
                Flag = ProjectTraceFlags.OverdueMeetingAction,
                Title = row.Title,
                Due = row.DueDate,
                WbsId = row.WbsId,
                Severity = "alert"
            });
        }

        return actions.Take(PulseActionLimit).ToList();
    }

    private static IReadOnlyList<ProjectPulseMilestoneDto> BuildUpcomingMilestones(
        IReadOnlyList<WbsItemDto> wbs,
        DateTime today)
    {
        var until = today.AddDays(PulseMilestoneDays);
        return wbs
            .Where(row => string.Equals(row.Kind, PmWbsKind.Milestone, StringComparison.OrdinalIgnoreCase))
            .Select(row =>
            {
                var finish = row.PlannedFinish?.ToUniversalTime().Date;
                var incomplete = row.PercentComplete < 99.5 && !row.WorkItemClosed;
                var delayed = incomplete && finish is not null && finish.Value < today;
                var atRisk = incomplete
                    && finish is not null
                    && finish.Value <= today.AddDays(MilestoneRiskDays);
                return new { row, finish, incomplete, delayed, atRisk };
            })
            .Where(x => x.finish is not null && (x.delayed || (x.finish.Value >= today && x.finish.Value <= until)))
            .OrderBy(x => x.finish)
            .Take(6)
            .Select(x => new ProjectPulseMilestoneDto
            {
                WbsId = x.row.Id,
                WbsCode = x.row.WbsCode,
                Name = x.row.Name,
                PlannedFinish = x.row.PlannedFinish,
                PercentComplete = x.row.PercentComplete,
                AtRisk = x.atRisk,
                Delayed = x.delayed
            })
            .ToList();
    }
}
