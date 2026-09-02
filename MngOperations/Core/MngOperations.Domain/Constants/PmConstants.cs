namespace MngOperations.Domain.Constants;

public static class PmDatasets
{
    public const string Projects = "pm_projects";
    public const string WbsItems = "pm_wbs_items";
    public const string Dependencies = "pm_dependencies";
    public const string Decisions = "pm_decisions";
    public const string ProjectPacks = "pm_project_packs";
    public const string StageGates = "pm_stage_gates";
    public const string RaidItems = "pm_raid_items";
    public const string ResourceAssignments = "pm_resource_assignments";
    public const string BudgetLines = "pm_budget_lines";
    public const string Acknowledgements = "pm_acknowledgements";
    public const string Obligations = "pm_obligations";
    public const string AuditPacks = "pm_audit_packs";
    public const string Meetings = "pm_meetings";
    public const string MeetingActions = "pm_meeting_actions";
    public const string Stakeholders = "pm_stakeholders";
    public const string ProcessMaps = "pm_process_maps";
}

public static class PmProjectStatus
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Closed = "closed";

    public static string Normalize(string? status)
    {
        var s = status?.Trim().ToLowerInvariant();
        return s switch
        {
            Active => Active,
            Closed => Closed,
            _ => Draft
        };
    }
}

public static class PmWbsKind
{
    public const string Summary = "summary";
    public const string Task = "task";
    public const string Milestone = "milestone";

    public static string Normalize(string? kind)
    {
        var k = kind?.Trim().ToLowerInvariant();
        return k switch
        {
            Summary => Summary,
            Milestone => Milestone,
            _ => Task
        };
    }
}

public static class PmDependencyType
{
    public const string FinishToStart = "FS";

    public static string Normalize(string? type)
    {
        var t = type?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(t) ? FinishToStart : t;
    }
}

public static class PmDecisionKind
{
    public const string General = "general";
    public const string ScopeChange = "scopeChange";

    public static string Normalize(string? kind)
    {
        var k = kind?.Trim();
        if (string.Equals(k, ScopeChange, StringComparison.OrdinalIgnoreCase))
            return ScopeChange;
        return General;
    }
}

public static class PmDecisionStatus
{
    public const string Open = "open";
    public const string Accepted = "accepted";
    public const string Superseded = "superseded";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, Accepted, StringComparison.OrdinalIgnoreCase))
            return Accepted;
        if (string.Equals(s, Superseded, StringComparison.OrdinalIgnoreCase))
            return Superseded;
        return Open;
    }
}

public static class PmStageGateStatus
{
    public const string Open = "open";
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Waived = "waived";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, Passed, StringComparison.OrdinalIgnoreCase))
            return Passed;
        if (string.Equals(s, Failed, StringComparison.OrdinalIgnoreCase))
            return Failed;
        if (string.Equals(s, Waived, StringComparison.OrdinalIgnoreCase))
            return Waived;
        return Open;
    }
}

public static class PmRaidKind
{
    public const string Risk = "risk";
    public const string Assumption = "assumption";
    public const string Issue = "issue";
    public const string Dependency = "dependency";

    public static bool TryNormalize(string? kind, out string value)
    {
        var k = kind?.Trim();
        if (string.Equals(k, Risk, StringComparison.OrdinalIgnoreCase)) { value = Risk; return true; }
        if (string.Equals(k, Assumption, StringComparison.OrdinalIgnoreCase)) { value = Assumption; return true; }
        if (string.Equals(k, Issue, StringComparison.OrdinalIgnoreCase)) { value = Issue; return true; }
        if (string.Equals(k, Dependency, StringComparison.OrdinalIgnoreCase)) { value = Dependency; return true; }
        value = string.Empty;
        return false;
    }
}

public static class PmRaidStatus
{
    public const string Open = "open";
    public const string Mitigating = "mitigating";
    public const string Closed = "closed";
    public const string Validated = "validated";
    public const string Invalid = "invalid";
    public const string InProgress = "inProgress";
    public const string Waiting = "waiting";
    public const string Resolved = "resolved";

    public static string Normalize(string kind, string? status)
    {
        var s = status?.Trim();
        return kind switch
        {
            PmRaidKind.Risk when Eq(s, Mitigating) => Mitigating,
            PmRaidKind.Risk when Eq(s, Closed) => Closed,
            PmRaidKind.Assumption when Eq(s, Validated) => Validated,
            PmRaidKind.Assumption when Eq(s, Invalid) => Invalid,
            PmRaidKind.Issue when Eq(s, InProgress) => InProgress,
            PmRaidKind.Issue when Eq(s, Closed) => Closed,
            PmRaidKind.Dependency when Eq(s, Waiting) => Waiting,
            PmRaidKind.Dependency when Eq(s, Resolved) => Resolved,
            _ => Open
        };
    }

    public static bool IsOpen(string kind, string status) => kind switch
    {
        PmRaidKind.Risk => Eq(status, Open) || Eq(status, Mitigating),
        PmRaidKind.Assumption => Eq(status, Open),
        PmRaidKind.Issue => Eq(status, Open) || Eq(status, InProgress),
        PmRaidKind.Dependency => Eq(status, Open) || Eq(status, Waiting),
        _ => Eq(status, Open)
    };

    private static bool Eq(string? a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

public static class PmRaidLevel
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    public static string Normalize(string? value)
    {
        var v = value?.Trim();
        if (string.Equals(v, Low, StringComparison.OrdinalIgnoreCase)) return Low;
        if (string.Equals(v, High, StringComparison.OrdinalIgnoreCase)) return High;
        return Medium;
    }

    public static int Score(string? value) => Normalize(value) switch
    {
        Low => 1,
        High => 3,
        _ => 2
    };
}

public static class PmRaidResponse
{
    public const string None = "none";
    public const string Avoid = "avoid";
    public const string Mitigate = "mitigate";
    public const string Transfer = "transfer";
    public const string Accept = "accept";

    public static string Normalize(string? value)
    {
        var v = value?.Trim();
        if (string.Equals(v, Avoid, StringComparison.OrdinalIgnoreCase)) return Avoid;
        if (string.Equals(v, Mitigate, StringComparison.OrdinalIgnoreCase)) return Mitigate;
        if (string.Equals(v, Transfer, StringComparison.OrdinalIgnoreCase)) return Transfer;
        if (string.Equals(v, Accept, StringComparison.OrdinalIgnoreCase)) return Accept;
        return None;
    }
}

public static class PmCapacity
{
    public const double WeeklyHours = 40;
    public const double OverloadEpsilon = 0.05;
}

public static class PmBudgetCategory
{
    public const string Labor = "labor";
    public const string Material = "material";
    public const string Subcontract = "subcontract";
    public const string Other = "other";

    public static bool TryNormalize(string? category, out string value)
    {
        var c = category?.Trim();
        if (string.Equals(c, Labor, StringComparison.OrdinalIgnoreCase)) { value = Labor; return true; }
        if (string.Equals(c, Material, StringComparison.OrdinalIgnoreCase)) { value = Material; return true; }
        if (string.Equals(c, Subcontract, StringComparison.OrdinalIgnoreCase)) { value = Subcontract; return true; }
        if (string.Equals(c, Other, StringComparison.OrdinalIgnoreCase)) { value = Other; return true; }
        value = string.Empty;
        return false;
    }
}

public static class PmBudgetMoney
{
    public const string DefaultCurrency = "TRY";
    public const double OverEpsilon = 0.005;
    public const double MaxAmount = 1_000_000_000;

    public static string NormalizeCurrency(string? value)
    {
        var c = (value ?? DefaultCurrency).Trim().ToUpperInvariant();
        if (c.Length != 3 || !c.All(char.IsAsciiLetter))
            return string.Empty;
        return c;
    }
}

public static class PmAckStatus
{
    public const string Pending = "pending";
    public const string Acknowledged = "acknowledged";
    public const string Waived = "waived";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, Acknowledged, StringComparison.OrdinalIgnoreCase)) return Acknowledged;
        if (string.Equals(s, Waived, StringComparison.OrdinalIgnoreCase)) return Waived;
        return Pending;
    }

    public static bool IsPending(string status) =>
        string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string status) =>
        string.Equals(status, Acknowledged, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Waived, StringComparison.OrdinalIgnoreCase);
}

public static class PmObligationStatus
{
    public const string Open = "open";
    public const string InProgress = "inProgress";
    public const string Satisfied = "satisfied";
    public const string Waived = "waived";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, InProgress, StringComparison.OrdinalIgnoreCase)) return InProgress;
        if (string.Equals(s, Satisfied, StringComparison.OrdinalIgnoreCase)) return Satisfied;
        if (string.Equals(s, Waived, StringComparison.OrdinalIgnoreCase)) return Waived;
        return Open;
    }

    public static bool IsOpen(string status) =>
        string.Equals(status, Open, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, InProgress, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string status) =>
        string.Equals(status, Satisfied, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Waived, StringComparison.OrdinalIgnoreCase);
}

public static class PmAuditPackKind
{
    public const string Audit = "audit";
    public const string Customer = "customer";
    public const string Internal = "internal";

    public static string Normalize(string? kind)
    {
        var k = kind?.Trim();
        if (string.Equals(k, Customer, StringComparison.OrdinalIgnoreCase)) return Customer;
        if (string.Equals(k, Internal, StringComparison.OrdinalIgnoreCase)) return Internal;
        return Audit;
    }
}

public static class PmAuditPackStatus
{
    public const string Draft = "draft";
    public const string Assembled = "assembled";
    public const string Issued = "issued";
    public const string Withdrawn = "withdrawn";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, Assembled, StringComparison.OrdinalIgnoreCase)) return Assembled;
        if (string.Equals(s, Issued, StringComparison.OrdinalIgnoreCase)) return Issued;
        if (string.Equals(s, Withdrawn, StringComparison.OrdinalIgnoreCase)) return Withdrawn;
        return Draft;
    }

    public static bool IsOpen(string status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Assembled, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string status) =>
        string.Equals(status, Issued, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Withdrawn, StringComparison.OrdinalIgnoreCase);
}

public static class PmMeetingActionStatus
{
    public const string Open = "open";
    public const string InProgress = "inProgress";
    public const string Done = "done";
    public const string Waived = "waived";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, InProgress, StringComparison.OrdinalIgnoreCase)) return InProgress;
        if (string.Equals(s, Done, StringComparison.OrdinalIgnoreCase)) return Done;
        if (string.Equals(s, Waived, StringComparison.OrdinalIgnoreCase)) return Waived;
        return Open;
    }

    public static bool IsOpen(string status) =>
        string.Equals(status, Open, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, InProgress, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string status) =>
        string.Equals(status, Done, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Waived, StringComparison.OrdinalIgnoreCase);
}

public static class PmStakeholderKind
{
    public const string Customer = "customer";
    public const string Supplier = "supplier";
    public const string Consultant = "consultant";
    public const string Regulator = "regulator";
    public const string Sponsor = "sponsor";
    public const string Other = "other";

    public static string Normalize(string? kind)
    {
        var k = kind?.Trim();
        if (string.Equals(k, Supplier, StringComparison.OrdinalIgnoreCase)) return Supplier;
        if (string.Equals(k, Consultant, StringComparison.OrdinalIgnoreCase)) return Consultant;
        if (string.Equals(k, Regulator, StringComparison.OrdinalIgnoreCase)) return Regulator;
        if (string.Equals(k, Sponsor, StringComparison.OrdinalIgnoreCase)) return Sponsor;
        if (string.Equals(k, Other, StringComparison.OrdinalIgnoreCase)) return Other;
        return Customer;
    }
}

public static class PmStakeholderStatus
{
    public const string Invited = "invited";
    public const string Active = "active";
    public const string Revoked = "revoked";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, Active, StringComparison.OrdinalIgnoreCase)) return Active;
        if (string.Equals(s, Revoked, StringComparison.OrdinalIgnoreCase)) return Revoked;
        return Invited;
    }

    public static bool IsOpen(string status) =>
        string.Equals(status, Invited, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string status) =>
        string.Equals(status, Revoked, StringComparison.OrdinalIgnoreCase);
}

public static class PmProcessMapKind
{
    public const string Procedure = "procedure";
    public const string Workflow = "workflow";
    public const string Org = "org";
    public const string Other = "other";

    public static string Normalize(string? kind)
    {
        var k = kind?.Trim();
        if (string.Equals(k, Workflow, StringComparison.OrdinalIgnoreCase)) return Workflow;
        if (string.Equals(k, Org, StringComparison.OrdinalIgnoreCase)) return Org;
        if (string.Equals(k, Other, StringComparison.OrdinalIgnoreCase)) return Other;
        return Procedure;
    }
}

public static class PmProcessMapStatus
{
    public const string Draft = "draft";
    public const string Current = "current";
    public const string Superseded = "superseded";

    public static string Normalize(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, Current, StringComparison.OrdinalIgnoreCase)) return Current;
        if (string.Equals(s, Superseded, StringComparison.OrdinalIgnoreCase)) return Superseded;
        return Draft;
    }

    public static bool IsOpen(string status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase);

    public static bool IsCurrent(string status) =>
        string.Equals(status, Current, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string status) =>
        string.Equals(status, Superseded, StringComparison.OrdinalIgnoreCase);
}
