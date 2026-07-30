using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.EventLog;

public static class DefaultEventLogPackages
{
    /// <summary>Optional auth package — requires LocalSystem/admin to read Security channel.</summary>
    public static EventLogPackage SecurityAuth { get; } = new()
    {
        Name = "security-auth",
        Channel = "Security",
        EventIds = [4624, 4625, 4634, 4648, 4672, 4720, 4726, 4740]
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

    /// <summary>Builds an XPath query for EventLogQuery (EventID filter + optional RecordId lower bound).</summary>
    public static string BuildQuery(EventLogPackage package, long? afterRecordIdExclusive)
    {
        var idFilter = string.Join(" or ", package.EventIds.Distinct().Select(id => $"EventID={id}"));
        if (afterRecordIdExclusive is > 0)
            return $"*[System[({idFilter}) and (EventRecordID > {afterRecordIdExclusive.Value})]]";

        return $"*[System[({idFilter})]]";
    }
}
