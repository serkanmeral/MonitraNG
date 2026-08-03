using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.EventLog;

public static class DefaultEventLogPackages
{
    /// <summary>Optional auth package — requires LocalSystem/admin to read Security channel.</summary>
    public static EventLogPackage SecurityAuth { get; } = new()
    {
        Name = "security-auth",
        Channel = "Security",
        IsDefault = false,
        EventIds = [4624, 4625, 4634, 4648, 4672, 4720, 4722, 4726, 4728, 4732, 4738, 4740, 5136, 5137, 5139]
    };

    /// <summary>RDP / local session (usually readable without admin).</summary>
    public static EventLogPackage RdpSession { get; } = new()
    {
        Name = "rdp-session",
        Channel = "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
        // 21 logon, 23 logoff, 24 disconnect, 25 reconnect
        EventIds = [21, 23, 24, 25]
    };

    /// <summary>Built-in packages when policy.Packages is empty (no Security — works without elevation).</summary>
    public static IReadOnlyList<EventLogPackage> Defaults { get; } =
    [
        new EventLogPackage
        {
            Name = "system-lifecycle",
            Channel = "System",
            EventIds =
            [
                41,    // Kernel-Power unexpected shutdown
                104,   // log cleared
                6005,  // Event Log service started
                6006,  // Event Log service stopped
                7031,  // service terminated unexpectedly (+ recovery)
                7034,  // service terminated unexpectedly
                7036,  // service entered running/stopped state
                7040,  // service start type changed
                7045   // service installed
            ]
        },
        new EventLogPackage
        {
            Name = "application-signals",
            Channel = "Application",
            EventIds =
            [
                1000, // Application Error / MngLogsPilot test writes
                1001, // Windows Error Reporting
                1026  // .NET Runtime
            ]
        },
        new EventLogPackage
        {
            // Easy non-admin samples: starting powershell.exe emits these.
            Name = "powershell-engine",
            Channel = "Windows PowerShell",
            EventIds = [400, 403, 600]
        },
        RdpSession
    ];

    /// <summary>All known built-ins (defaults + optional Security).</summary>
    public static IReadOnlyList<EventLogPackage> AllKnown { get; } =
    [
        ..Defaults,
        SecurityAuth
    ];

    /// <summary>Legacy alias — same as <see cref="AllKnown"/>.</summary>
    public static IReadOnlyList<EventLogPackage> All => AllKnown;

    /// <summary>
    /// Resolves the effective package set: server catalog ⊕ agent overrides (− disabled).
    /// Legacy: when <see cref="EventLogPolicy.Packages"/> is non-empty and overrides/disabled are unused,
    /// Packages alone is returned (old full-replace behavior).
    /// </summary>
    public static IReadOnlyList<EventLogPackage> Resolve(
        EventLogPolicy policy,
        IReadOnlyList<EventLogPackage>? serverCatalog = null)
    {
        var server = serverCatalog is { Count: > 0 }
            ? serverCatalog
            : Defaults;

        var hasOverrides = policy.AgentOverrides is { Count: > 0 };
        var hasDisabled = policy.DisabledServerPackages is { Count: > 0 };
        var hasLegacyPackages = policy.Packages is { Count: > 0 };

        // Legacy full-replace mode
        if (hasLegacyPackages && !hasOverrides && !hasDisabled)
        {
            return policy.Packages
                .Where(EventLogPackageMerger.IsValid)
                .Select(EventLogPackageMerger.Clone)
                .ToList();
        }

        return EventLogPackageMerger.Merge(server, policy.AgentOverrides, policy.DisabledServerPackages);
    }

    /// <summary>
    /// Builds an XPath query for EventLogQuery.
    /// <c>selected</c>: EventID include list; <c>all</c>: whole channel with optional EventID excludes.
    /// </summary>
    public static string BuildQuery(EventLogPackage package, long? afterRecordIdExclusive)
    {
        var predicates = new List<string>();

        if (package.IsAllChannel)
        {
            var excludes = (package.ExcludedEventIds ?? [])
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .Select(id => $"EventID!={id}")
                .ToList();
            if (excludes.Count > 0)
                predicates.Add(excludes.Count == 1
                    ? excludes[0]
                    : $"({string.Join(" and ", excludes)})");
        }
        else
        {
            var idFilter = string.Join(
                " or ",
                (package.EventIds ?? []).Distinct().Select(id => $"EventID={id}"));
            if (string.IsNullOrWhiteSpace(idFilter))
                throw new ArgumentException("selected package requires at least one Event ID.", nameof(package));
            predicates.Add($"({idFilter})");
        }

        if (afterRecordIdExclusive is > 0)
            predicates.Add($"(EventRecordID > {afterRecordIdExclusive.Value})");

        if (predicates.Count == 0)
            return "*";

        return $"*[System[{string.Join(" and ", predicates)}]]";
    }

    /// <summary>
    /// Package filter + recent time window (Windows Event Log <c>timediff</c> in ms).
    /// Used to locate the oldest in-window record when seeding a history cursor.
    /// </summary>
    public static string BuildHistoryWindowQuery(EventLogPackage package, int lookbackHours)
    {
        var hours = Math.Clamp(lookbackHours, 1, 168);
        var ms = (long)hours * 3_600_000L;
        var baseQuery = BuildQuery(package, afterRecordIdExclusive: null);
        // Inject TimeCreated into System predicates.
        if (baseQuery == "*")
            return $"*[System[TimeCreated[timediff(@SystemTime) <= {ms}]]]";

        const string prefix = "*[System[";
        const string suffix = "]]";
        if (baseQuery.StartsWith(prefix, StringComparison.Ordinal) &&
            baseQuery.EndsWith(suffix, StringComparison.Ordinal))
        {
            var inner = baseQuery[prefix.Length..^suffix.Length];
            return $"*[System[{inner} and TimeCreated[timediff(@SystemTime) <= {ms}]]]";
        }

        return $"*[System[TimeCreated[timediff(@SystemTime) <= {ms}]]]";
    }
}
