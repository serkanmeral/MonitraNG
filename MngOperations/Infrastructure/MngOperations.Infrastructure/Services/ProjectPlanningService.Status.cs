using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const string DmResourceLinks = "dm_resource_links";
    private const string DmResources = "dm_resources";
    private const int MilestoneRiskDays = 7;

    public async Task<ProjectStatusPackDto> GetStatusPackAsync(string projectId, CancellationToken ct = default)
    {
        var detail = await GetProjectAsync(projectId, ct);
        var token = RequireToken();
        var today = DateTime.UtcNow.Date;
        var childIds = detail.Wbs
            .Where(w => !string.IsNullOrWhiteSpace(w.ParentId))
            .Select(w => w.ParentId!)
            .ToHashSet(StringComparer.Ordinal);

        var workItemIds = detail.Wbs
            .Select(w => w.WorkItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var docsByWorkItem = await LoadDocumentsByWorkItemAsync(workItemIds, token, ct);
        var decisions = detail.Decisions;
        var gates = detail.StageGates;
        var raid = detail.RaidItems;
        var capacity = detail.Capacity;
        var overloadedKeys = capacity.People
            .Where(p => p.Overloaded)
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);
        var overloadedWbs = capacity.Assignments
            .Where(a => overloadedKeys.Contains(ResourceKey(a.PersonId, a.Name)))
            .Select(a => a.WbsId)
            .ToHashSet(StringComparer.Ordinal);
        var budget = detail.Budget;
        var overBudgetWbs = budget.Packages
            .Where(p => p.Over)
            .Select(p => p.WbsId)
            .ToHashSet(StringComparer.Ordinal);
        var acknowledgements = BuildAcknowledgements(detail.Acknowledgements);
        var pendingAckWbs = acknowledgements.Items
            .Where(a => a.Pending && !string.IsNullOrWhiteSpace(a.WbsId))
            .Select(a => a.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var overdueAckWbs = acknowledgements.Items
            .Where(a => a.Overdue && !string.IsNullOrWhiteSpace(a.WbsId))
            .Select(a => a.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var obligations = BuildObligations(detail.Obligations);
        var openObligationWbs = obligations.Items
            .Where(o => o.Open && !string.IsNullOrWhiteSpace(o.WbsId))
            .Select(o => o.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var overdueObligationWbs = obligations.Items
            .Where(o => o.Overdue && !string.IsNullOrWhiteSpace(o.WbsId))
            .Select(o => o.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var unboundObligationWbs = obligations.Items
            .Where(o => o.Unbound && !string.IsNullOrWhiteSpace(o.WbsId))
            .Select(o => o.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var auditPacks = BuildAuditPacks(detail.AuditPacks);
        var openAuditWbs = auditPacks.Items
            .Where(p => p.Open && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var incompleteAuditWbs = auditPacks.Items
            .Where(p => p.Incomplete && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var overdueAuditWbs = auditPacks.Items
            .Where(p => p.Overdue && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var meetingActions = BuildMeetingActions(detail.Meetings);
        var meetingById = detail.Meetings.ToDictionary(m => m.Id, StringComparer.Ordinal);
        var openMeetingWbs = new HashSet<string>(StringComparer.Ordinal);
        var overdueMeetingWbs = new HashSet<string>(StringComparer.Ordinal);
        var unboundMeetingWbs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in meetingActions.Items)
        {
            var meetingWbs = meetingById.TryGetValue(action.MeetingId, out var meeting) ? meeting.WbsId : null;
            var wbsId = !string.IsNullOrWhiteSpace(action.WbsId) ? action.WbsId : meetingWbs;
            if (string.IsNullOrWhiteSpace(wbsId)) continue;
            if (action.Open) openMeetingWbs.Add(wbsId);
            if (action.Overdue) overdueMeetingWbs.Add(wbsId);
            if (action.Unbound) unboundMeetingWbs.Add(wbsId);
        }
        var stakeholders = BuildStakeholders(detail.Stakeholders);
        var openStakeholderWbs = stakeholders.Items
            .Where(p => p.Open && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var incompleteStakeholderWbs = stakeholders.Items
            .Where(p => p.Incomplete && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var overdueStakeholderWbs = stakeholders.Items
            .Where(p => p.Overdue && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var processMaps = BuildProcessMaps(detail.ProcessMaps);
        var openProcessMapWbs = processMaps.Items
            .Where(p => p.Open && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var incompleteProcessMapWbs = processMaps.Items
            .Where(p => p.Incomplete && !string.IsNullOrWhiteSpace(p.WbsId))
            .Select(p => p.WbsId!)
            .ToHashSet(StringComparer.Ordinal);
        var rows = new List<ProjectTraceRowDto>();

        foreach (var wbs in detail.Wbs)
        {
            var docs = string.IsNullOrWhiteSpace(wbs.WorkItemId)
                ? (IReadOnlyList<TraceDocumentDto>)Array.Empty<TraceDocumentDto>()
                : docsByWorkItem.GetValueOrDefault(wbs.WorkItemId) ?? (IReadOnlyList<TraceDocumentDto>)Array.Empty<TraceDocumentDto>();
            var isLeaf = !childIds.Contains(wbs.Id);
            var flags = BuildFlags(
                wbs,
                docs,
                isLeaf,
                today,
                gates,
                raid,
                overloadedWbs.Contains(wbs.Id),
                overBudgetWbs.Contains(wbs.Id),
                pendingAckWbs.Contains(wbs.Id),
                overdueAckWbs.Contains(wbs.Id),
                openObligationWbs.Contains(wbs.Id),
                overdueObligationWbs.Contains(wbs.Id),
                unboundObligationWbs.Contains(wbs.Id),
                openAuditWbs.Contains(wbs.Id),
                incompleteAuditWbs.Contains(wbs.Id),
                overdueAuditWbs.Contains(wbs.Id),
                openMeetingWbs.Contains(wbs.Id),
                overdueMeetingWbs.Contains(wbs.Id),
                unboundMeetingWbs.Contains(wbs.Id),
                openStakeholderWbs.Contains(wbs.Id),
                incompleteStakeholderWbs.Contains(wbs.Id),
                overdueStakeholderWbs.Contains(wbs.Id),
                openProcessMapWbs.Contains(wbs.Id),
                incompleteProcessMapWbs.Contains(wbs.Id));
            var related = decisions
                .Where(d =>
                    d.WbsIds.Contains(wbs.Id, StringComparer.Ordinal)
                    || (!string.IsNullOrWhiteSpace(wbs.WorkItemId)
                        && d.WorkItemIds.Contains(wbs.WorkItemId, StringComparer.Ordinal)))
                .ToList();
            var relatedRaid = raid
                .Where(r => r.WbsIds.Contains(wbs.Id, StringComparer.Ordinal)
                    || (!string.IsNullOrWhiteSpace(wbs.WorkItemId)
                        && r.WorkItemIds.Contains(wbs.WorkItemId, StringComparer.Ordinal)))
                .ToList();
            rows.Add(new ProjectTraceRowDto
            {
                WbsId = wbs.Id,
                WbsCode = wbs.WbsCode,
                WbsName = wbs.Name,
                Kind = wbs.Kind,
                PercentComplete = wbs.PercentComplete,
                PlannedFinish = wbs.PlannedFinish,
                BaselineDrifted = wbs.BaselineDrifted,
                WorkItemId = wbs.WorkItemId,
                WorkItemKey = wbs.WorkItemKey,
                WorkItemTitle = wbs.WorkItemTitle,
                WorkItemStateName = wbs.WorkItemStateName,
                WorkItemClosed = wbs.WorkItemClosed,
                Documents = docs,
                Flags = flags,
                Decisions = related,
                RaidItems = relatedRaid
            });
        }

        return new ProjectStatusPackDto
        {
            ProjectId = projectId,
            GeneratedAt = DateTime.UtcNow,
            Counts = new ProjectStatusCountsDto
            {
                Delayed = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.Delayed)),
                MilestoneAtRisk = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.MilestoneAtRisk)),
                Drifted = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.Drifted)),
                UnboundLeaf = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.Unbound)),
                OpenWork = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.OpenWork)),
                MissingEvidence = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.MissingEvidence)),
                MissingApproval = rows.Count(r => r.Flags.Contains(ProjectTraceFlags.MissingApproval)),
                OpenScopeChange = decisions.Count(d =>
                    string.Equals(d.Kind, PmDecisionKind.ScopeChange, StringComparison.Ordinal)
                    && string.Equals(d.Status, PmDecisionStatus.Open, StringComparison.Ordinal)),
                OpenGate = gates.Count(g => string.Equals(g.Status, PmStageGateStatus.Open, StringComparison.Ordinal)),
                FailedGate = gates.Count(g => string.Equals(g.Status, PmStageGateStatus.Failed, StringComparison.Ordinal)),
                OpenRisk = raid.Count(r => r.Kind == PmRaidKind.Risk && r.Open && r.Elevated),
                OpenIssue = raid.Count(r => r.Kind == PmRaidKind.Issue && r.Open),
                OpenAssumption = raid.Count(r => r.Kind == PmRaidKind.Assumption && r.Open),
                OpenDependency = raid.Count(r => r.Kind == PmRaidKind.Dependency && r.Open),
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
                UnboundMeetingAction = meetingActions.UnboundCount,
                OpenStakeholder = stakeholders.OpenCount,
                IncompleteStakeholder = stakeholders.IncompleteCount,
                OverdueStakeholder = stakeholders.OverdueCount,
                OpenProcessMap = processMaps.OpenCount,
                IncompleteProcessMap = processMaps.IncompleteCount,
                CurrentProcessMap = processMaps.CurrentCount
            },
            Items = rows,
            Gates = gates,
            RaidItems = raid,
            Capacity = capacity,
            Budget = budget,
            Acknowledgements = acknowledgements,
            Obligations = obligations,
            AuditPacks = auditPacks,
            MeetingActions = meetingActions,
            Stakeholders = stakeholders,
            ProcessMaps = processMaps
        };
    }

    private static IReadOnlyList<string> BuildFlags(
        WbsItemDto wbs,
        IReadOnlyList<TraceDocumentDto> docs,
        bool isLeaf,
        DateTime today,
        IReadOnlyList<StageGateDto> gates,
        IReadOnlyList<RaidItemDto> raid,
        bool overloaded,
        bool overBudget,
        bool pendingAck,
        bool overdueAck,
        bool openObligation,
        bool overdueObligation,
        bool unboundObligation,
        bool openAuditPack,
        bool incompleteAuditPack,
        bool overdueAuditPack,
        bool openMeetingAction,
        bool overdueMeetingAction,
        bool unboundMeetingAction,
        bool openStakeholder,
        bool incompleteStakeholder,
        bool overdueStakeholder,
        bool openProcessMap,
        bool incompleteProcessMap)
    {
        var flags = new List<string>();
        var incomplete = wbs.PercentComplete < 99.5 && !wbs.WorkItemClosed;
        var finish = wbs.PlannedFinish?.ToUniversalTime().Date;

        if (incomplete && finish is not null && finish.Value < today)
            flags.Add(ProjectTraceFlags.Delayed);

        if (string.Equals(wbs.Kind, PmWbsKind.Milestone, StringComparison.OrdinalIgnoreCase)
            && incomplete
            && finish is not null
            && finish.Value <= today.AddDays(MilestoneRiskDays))
        {
            flags.Add(ProjectTraceFlags.MilestoneAtRisk);
        }

        if (wbs.BaselineDrifted)
            flags.Add(ProjectTraceFlags.Drifted);

        if (isLeaf && string.IsNullOrWhiteSpace(wbs.WorkItemId))
            flags.Add(ProjectTraceFlags.Unbound);

        if (!string.IsNullOrWhiteSpace(wbs.WorkItemId) && incomplete)
            flags.Add(ProjectTraceFlags.OpenWork);

        if (!string.IsNullOrWhiteSpace(wbs.WorkItemId)
            && !docs.Any(d => IsEvidenceRelation(d.RelationType)))
        {
            flags.Add(ProjectTraceFlags.MissingEvidence);
        }

        if (docs.Any(d => !d.Approved))
            flags.Add(ProjectTraceFlags.MissingApproval);

        var bound = gates.Where(g => string.Equals(g.WbsId, wbs.Id, StringComparison.Ordinal)).ToList();
        if (bound.Any(g => string.Equals(g.Status, PmStageGateStatus.Open, StringComparison.Ordinal)))
            flags.Add(ProjectTraceFlags.OpenGate);
        if (bound.Any(g => string.Equals(g.Status, PmStageGateStatus.Failed, StringComparison.Ordinal)))
            flags.Add(ProjectTraceFlags.FailedGate);

        var boundRaid = raid.Where(r => r.Open && r.WbsIds.Contains(wbs.Id, StringComparer.Ordinal)).ToList();
        if (boundRaid.Any(r => r.Kind == PmRaidKind.Risk && r.Elevated))
            flags.Add(ProjectTraceFlags.OpenRisk);
        if (boundRaid.Any(r => r.Kind == PmRaidKind.Issue))
            flags.Add(ProjectTraceFlags.OpenIssue);

        if (overloaded)
            flags.Add(ProjectTraceFlags.OverloadedResource);

        if (overBudget)
            flags.Add(ProjectTraceFlags.OverBudget);

        if (pendingAck)
            flags.Add(ProjectTraceFlags.PendingAck);
        if (overdueAck)
            flags.Add(ProjectTraceFlags.OverdueAck);
        if (openObligation)
            flags.Add(ProjectTraceFlags.OpenObligation);
        if (overdueObligation)
            flags.Add(ProjectTraceFlags.OverdueObligation);
        if (unboundObligation)
            flags.Add(ProjectTraceFlags.UnboundObligation);
        if (openAuditPack)
            flags.Add(ProjectTraceFlags.OpenAuditPack);
        if (incompleteAuditPack)
            flags.Add(ProjectTraceFlags.IncompleteAuditPack);
        if (overdueAuditPack)
            flags.Add(ProjectTraceFlags.OverdueAuditPack);
        if (openMeetingAction)
            flags.Add(ProjectTraceFlags.OpenMeetingAction);
        if (overdueMeetingAction)
            flags.Add(ProjectTraceFlags.OverdueMeetingAction);
        if (unboundMeetingAction)
            flags.Add(ProjectTraceFlags.UnboundMeetingAction);
        if (openStakeholder)
            flags.Add(ProjectTraceFlags.OpenStakeholder);
        if (incompleteStakeholder)
            flags.Add(ProjectTraceFlags.IncompleteStakeholder);
        if (overdueStakeholder)
            flags.Add(ProjectTraceFlags.OverdueStakeholder);
        if (openProcessMap)
            flags.Add(ProjectTraceFlags.OpenProcessMap);
        if (incompleteProcessMap)
            flags.Add(ProjectTraceFlags.IncompleteProcessMap);

        return flags;
    }

    private static bool IsEvidenceRelation(string? relationType) =>
        string.Equals(relationType, "evidence", StringComparison.OrdinalIgnoreCase)
        || string.Equals(relationType, "output", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDocStatus(string? status)
    {
        if (string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase))
            return "draft";
        if (string.Equals(status, "inReview", StringComparison.OrdinalIgnoreCase))
            return "inReview";
        return "published";
    }

    private async Task<Dictionary<string, List<TraceDocumentDto>>> LoadDocumentsByWorkItemAsync(
        IReadOnlyList<string> workItemIds,
        string token,
        CancellationToken ct)
    {
        var result = new Dictionary<string, List<TraceDocumentDto>>(StringComparer.Ordinal);
        if (workItemIds.Count == 0)
            return result;

        try
        {
            var linksPage = await _dg.QueryPageAsync(
                DmResourceLinks,
                new Dictionary<string, object?>
                {
                    ["targetModule"] = "operationCore",
                    ["targetType"] = "workItem",
                    ["targetId"] = new Dictionary<string, object?> { ["$in"] = workItemIds.Cast<object?>().ToList() }
                },
                "limit=500&expand=false",
                token,
                ct);

            var resourceIds = linksPage.Items
                .Select(row => WorkItemDataHelper.GetString(row, "resourceId"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var resources = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
            if (resourceIds.Count > 0)
            {
                var resPage = await _dg.QueryPageAsync(
                    DmResources,
                    new Dictionary<string, object?>
                    {
                        ["__dataId"] = new Dictionary<string, object?> { ["$in"] = resourceIds.Cast<object?>().ToList() }
                    },
                    "limit=500&expand=false",
                    token,
                    ct);
                foreach (var row in resPage.Items)
                {
                    var id = WorkItemDataHelper.GetDataId(row);
                    if (!string.IsNullOrWhiteSpace(id))
                        resources[id] = row;
                }
            }

            foreach (var link in linksPage.Items)
            {
                var workItemId = WorkItemDataHelper.GetString(link, "targetId");
                var resourceId = WorkItemDataHelper.GetString(link, "resourceId");
                if (string.IsNullOrWhiteSpace(workItemId) || string.IsNullOrWhiteSpace(resourceId))
                    continue;

                resources.TryGetValue(resourceId, out var resource);
                var status = NormalizeDocStatus(resource is null ? null : WorkItemDataHelper.GetString(resource, "status"));
                var name = resource is null
                    ? resourceId
                    : WorkItemDataHelper.GetString(resource, "title")
                      ?? WorkItemDataHelper.GetString(resource, "name")
                      ?? resourceId;

                if (!result.TryGetValue(workItemId, out var list))
                {
                    list = [];
                    result[workItemId] = list;
                }

                list.Add(new TraceDocumentDto
                {
                    ResourceId = resourceId,
                    Name = name,
                    Kind = resource is null ? null : WorkItemDataHelper.GetString(resource, "kind"),
                    RelationType = WorkItemDataHelper.GetString(link, "relationType") ?? "reference",
                    Status = status,
                    Approved = status == "published"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DI document lookup failed for project status pack (non-fatal)");
        }

        return result;
    }
}
