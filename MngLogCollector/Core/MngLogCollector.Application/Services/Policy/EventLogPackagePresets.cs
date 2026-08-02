using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Application.Services.Policy;

/// <summary>Starter package templates for Settings (prefill create form; not auto-published).</summary>
public static class EventLogPackagePresets
{
    public static IReadOnlyList<EventLogPackagePresetDto> All { get; } =
    [
        new()
        {
            Id = "ad-auth-core",
            Title = "AD / Auth (Security)",
            Description = "Logon success/fail, Kerberos, account lockout, privilege use.",
            SuggestedName = "ad-auth-core",
            Channel = "Security",
            IsDefault = false,
            EventIds = [4624, 4625, 4634, 4648, 4672, 4740, 4768, 4769, 4771, 4776]
        },
        new()
        {
            Id = "ad-account-mgmt",
            Title = "AD Account management",
            Description = "User create/delete/disable, group membership, password reset.",
            SuggestedName = "ad-account-mgmt",
            Channel = "Security",
            IsDefault = false,
            EventIds = [4720, 4722, 4724, 4725, 4726, 4728, 4732, 4738, 4756, 4767]
        },
        new()
        {
            Id = "sql-app-errors",
            Title = "SQL Server (Application)",
            Description = "Common SQL login / error IDs in Application log (tune per instance).",
            SuggestedName = "sql-app-errors",
            Channel = "Application",
            IsDefault = false,
            EventIds = [18456, 17187, 17055, 26037, 1000, 1001]
        },
        new()
        {
            Id = "sysmon-core",
            Title = "Sysmon core",
            Description = "Requires Sysmon + Microsoft-Windows-Sysmon/Operational channel.",
            SuggestedName = "sysmon-core",
            Channel = "Microsoft-Windows-Sysmon/Operational",
            IsDefault = false,
            EventIds = [1, 3, 7, 8, 10, 11, 12, 13, 22]
        },
        new()
        {
            Id = "powershell-scriptblock",
            Title = "PowerShell ScriptBlock",
            Description = "Operational channel 4103/4104 (enable Script Block Logging).",
            SuggestedName = "powershell-scriptblock",
            Channel = "Microsoft-Windows-PowerShell/Operational",
            IsDefault = false,
            EventIds = [4103, 4104]
        },
        new()
        {
            Id = "rdp-sessions",
            Title = "RDP sessions (LSM)",
            Description = "Local Session Manager operational events.",
            SuggestedName = "rdp-sessions",
            Channel = "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational",
            IsDefault = false,
            EventIds = [21, 22, 23, 24, 25]
        }
    ];
}
