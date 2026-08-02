using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Application.Services.Policy;

/// <summary>
/// Known Event ID dictionary for Settings multi-select (curated, not exhaustive).
/// Custom channels remain allowed via free-text in the UI.
/// </summary>
public static class EventLogChannelDictionary
{
    public static IReadOnlyList<EventLogChannelDictionaryDto> All { get; } =
    [
        Chan("System", "System",
            (41, "Kernel-Power unexpected shutdown"),
            (104, "Event log cleared"),
            (6005, "Event log service started"),
            (6006, "Event log service stopped"),
            (7000, "Service failed to start"),
            (7001, "Service timeout"),
            (7023, "Service terminated with error"),
            (7031, "Service terminated unexpectedly"),
            (7034, "Service crashed"),
            (7036, "Service entered state"),
            (7040, "Service start type changed"),
            (7045, "Service installed")),
        Chan("Application", "Application",
            (1000, "Application error"),
            (1001, "Application hang"),
            (1026, ".NET Runtime error"),
            (18456, "SQL Server login failed"),
            (17187, "SQL Server login failed (extended)"),
            (26037, "SQL Server encryption / TLS"),
            (17055, "SQL Server error (generic)")),
        Chan("Security", "Security — AD / auth",
            (4624, "Logon success"),
            (4625, "Logon failed"),
            (4634, "Logoff"),
            (4647, "User initiated logoff"),
            (4648, "Logon with explicit credentials"),
            (4672, "Special privileges assigned"),
            (4688, "Process created"),
            (4697, "Service installed (Security)"),
            (4698, "Scheduled task created"),
            (4700, "Scheduled task enabled"),
            (4719, "System audit policy changed"),
            (4720, "User account created"),
            (4722, "User account enabled"),
            (4723, "Password change attempt"),
            (4724, "Password reset attempt"),
            (4725, "User account disabled"),
            (4726, "User account deleted"),
            (4728, "Member added to security-enabled global group"),
            (4732, "Member added to security-enabled local group"),
            (4738, "User account changed"),
            (4740, "Account locked out"),
            (4756, "Member added to universal group"),
            (4767, "Account unlocked"),
            (4768, "Kerberos TGT requested"),
            (4769, "Kerberos service ticket requested"),
            (4771, "Kerberos pre-auth failed"),
            (4776, "Credential validation (NTLM)"),
            (5140, "Network share accessed"),
            (5145, "Network share object checked")),
        Chan("Windows PowerShell", "Windows PowerShell (classic)",
            (400, "Engine state start"),
            (403, "Engine state stop"),
            (600, "Provider started")),
        Chan(
            "Microsoft-Windows-PowerShell/Operational",
            "PowerShell Operational",
            (4103, "Module logging / pipeline"),
            (4104, "Script block logging")),
        Chan(
            "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
            "RDP Session (LSM)",
            (21, "Session logon"),
            (22, "Shell start"),
            (23, "Session logoff"),
            (24, "Session disconnected"),
            (25, "Session reconnected")),
        Chan(
            "Microsoft-Windows-Sysmon/Operational",
            "Sysmon",
            (1, "Process Create"),
            (3, "Network connection"),
            (7, "Image loaded"),
            (8, "CreateRemoteThread"),
            (10, "ProcessAccess"),
            (11, "FileCreate"),
            (12, "RegistryEvent (create/delete)"),
            (13, "RegistryEvent (value set)"),
            (22, "DNS query"),
            (25, "ProcessTampering"))
    ];

    private static EventLogChannelDictionaryDto Chan(
        string channel,
        string label,
        params (int Id, string Label)[] ids) => new()
    {
        Channel = channel,
        Label = label,
        KnownEventIds = ids
            .Where(x => x.Id > 0 && x.Id < 1_000_000)
            .Select(x => new EventLogKnownIdDto { Id = x.Id, Label = x.Label })
            .ToList()
    };
}
