using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Services.SecEvents;

/// <summary>
/// Builtin parse rules. Bump <see cref="SeedRevision"/> when content changes so
/// <c>EnsureSeeded</c> refreshes existing tenant builtins.
/// </summary>
public static class SecEventParseRuleCatalogSeed
{
    public const string InitialVersion = "0";

    /// <summary>Increment when seed match/extract/name/description changes.</summary>
    public const int SeedRevision = 5;

    private const string WindowsProduct = "windows";
    private const string SecurityChannel = "Security";
    private const string RdpChannel =
        "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational";

    public static IReadOnlyList<SecEventParseRuleDocument> CreateSeedDocuments()
    {
        var now = DateTime.UtcNow;
        return
        [
            Rule(
                "windows.logon.4625",
                "Windows failed logon",
                "Security 4625 — failed interactive/network/RDP logon. Maps user, source IP, logon type, workstation.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "endpoint", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4625]
                },
                extract:
                [
                    Ed("TargetUserName", "actor.user"),
                    Ed("IpAddress", "network.srcIp"),
                    Ed("LogonType", "custom.logon_type"),
                    Ed("WorkstationName", "custom.workstation"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    C("event.action", "login_failed"),
                    C("event.outcome", "failure"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "windows.logon.4624",
                "Windows successful logon",
                "Security 4624 — successful logon. Maps user, source IP, logon type, workstation.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "endpoint", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4624]
                },
                extract:
                [
                    Ed("TargetUserName", "actor.user"),
                    Ed("IpAddress", "network.srcIp"),
                    Ed("LogonType", "custom.logon_type"),
                    Ed("WorkstationName", "custom.workstation"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    C("event.action", "login_success"),
                    C("event.outcome", "success"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "windows.account.4740",
                "Windows account locked",
                "Security 4740 — account lockout. Maps locked account and calling workstation.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4740]
                },
                extract:
                [
                    Ed("TargetUserName", "actor.user"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    Ed("WorkstationName", "custom.workstation"),
                    C("event.action", "account_locked"),
                    C("event.outcome", "failure"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "windows.account.4720",
                "Windows account created",
                "Security 4720 — new account. actor.user = creator (Subject); custom.target_user = new account.",
                priority: 110,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4720]
                },
                extract:
                [
                    Ed("SubjectUserName", "actor.user"),
                    Ed("TargetUserName", "custom.target_user"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    C("event.action", "account_created"),
                    C("event.outcome", "success"),
                    C("event.category", "authorization")
                ]),
            Rule(
                "windows.logoff.4634",
                "Windows logoff",
                "Security 4634 — account logoff. Maps user, domain, logon type.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "endpoint", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4634]
                },
                extract:
                [
                    Ed("TargetUserName", "actor.user"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    Ed("LogonType", "custom.logon_type"),
                    C("event.action", "logoff"),
                    C("event.outcome", "success"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "windows.logon.4648",
                "Windows explicit credentials",
                "Security 4648 — a process used explicit credentials. Subject = caller; Target = impersonated account.",
                priority: 110,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "endpoint", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4648]
                },
                extract:
                [
                    Ed("SubjectUserName", "actor.user"),
                    Ed("TargetUserName", "custom.target_user"),
                    Ed("TargetServerName", "custom.target_server"),
                    Ed("IpAddress", "network.srcIp"),
                    C("event.action", "explicit_credentials"),
                    C("event.outcome", "success"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "windows.privilege.4672",
                "Windows special privileges assigned",
                "Security 4672 — special privileges assigned to new logon.",
                priority: 120,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "endpoint", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4672]
                },
                extract:
                [
                    Ed("SubjectUserName", "actor.user"),
                    Ed("SubjectDomainName", "custom.target_domain"),
                    Ed("PrivilegeList", "custom.privilege_list"),
                    C("event.action", "privileged_assigned"),
                    C("event.outcome", "success"),
                    C("event.category", "authorization")
                ]),
            Rule(
                "windows.account.4726",
                "Windows account deleted",
                "Security 4726 — user account deleted. Subject = who deleted; Target = deleted account.",
                priority: 110,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4726]
                },
                extract:
                [
                    Ed("SubjectUserName", "actor.user"),
                    Ed("TargetUserName", "custom.target_user"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    C("event.action", "account_deleted"),
                    C("event.outcome", "success"),
                    C("event.category", "authorization")
                ]),
            Rule(
                "windows.account.4722",
                "Windows account enabled",
                "Security 4722 — user account enabled.",
                priority: 110,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4722]
                },
                extract:
                [
                    Ed("SubjectUserName", "actor.user"),
                    Ed("TargetUserName", "custom.target_user"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    C("event.action", "account_enabled"),
                    C("event.outcome", "success"),
                    C("event.category", "authorization")
                ]),
            Rule(
                "windows.group.4728",
                "Windows global group member added",
                "Security 4728 — member added to security-enabled global group.",
                priority: 110,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4728]
                },
                extract: GroupMemberExtract()),
            Rule(
                "windows.group.4732",
                "Windows local group member added",
                "Security 4732 — member added to security-enabled local group.",
                priority: 110,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4732]
                },
                extract: GroupMemberExtract()),
            Rule(
                "windows.group.4738",
                "Windows group changed",
                "Security 4738 — security-enabled group changed.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [4738]
                },
                extract:
                [
                    Ed("SubjectUserName", "actor.user"),
                    Ed("TargetUserName", "custom.group"),
                    Ed("TargetDomainName", "custom.target_domain"),
                    C("event.action", "group_changed"),
                    C("event.outcome", "success"),
                    C("event.category", "authorization")
                ]),
            Rule(
                "windows.directory.5136",
                "Windows directory object modified",
                "Security 5136 — a directory service object was modified.",
                priority: 90,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [5136]
                },
                extract: DirectoryObjectExtract("directory_object_modified")),
            Rule(
                "windows.directory.5137",
                "Windows directory object created",
                "Security 5137 — a directory service object was created.",
                priority: 90,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [5137]
                },
                extract: DirectoryObjectExtract("directory_object_created")),
            Rule(
                "windows.directory.5139",
                "Windows directory object deleted",
                "Security 5139 — a directory service object was deleted.",
                priority: 90,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["ad", "windows-eventlog"],
                    Channel = [SecurityChannel],
                    EventIds = [5139]
                },
                extract: DirectoryObjectExtract("directory_object_deleted")),
            Rule(
                "windows.rdp.21",
                "RDP session logon",
                "LocalSessionManager 21 — remote desktop session logon (User / Address / SessionID).",
                priority: 120,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["endpoint", "windows-eventlog"],
                    Channel = [RdpChannel],
                    EventIds = [21]
                },
                extract: RdpExtract("rdp.logon", "success")),
            Rule(
                "windows.rdp.23",
                "RDP session logoff",
                "LocalSessionManager 23 — remote desktop session logoff.",
                priority: 120,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["endpoint", "windows-eventlog"],
                    Channel = [RdpChannel],
                    EventIds = [23]
                },
                extract: RdpExtract("rdp.logoff", "success")),
            Rule(
                "windows.rdp.24",
                "RDP session disconnected",
                "LocalSessionManager 24 — remote desktop session disconnected.",
                priority: 120,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["endpoint", "windows-eventlog"],
                    Channel = [RdpChannel],
                    EventIds = [24]
                },
                extract: RdpExtract("rdp.disconnect", "success")),
            Rule(
                "windows.rdp.25",
                "RDP session reconnected",
                "LocalSessionManager 25 — remote desktop session reconnected.",
                priority: 120,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = [WindowsProduct],
                    SourceType = ["endpoint", "windows-eventlog"],
                    Channel = [RdpChannel],
                    EventIds = [25]
                },
                extract: RdpExtract("rdp.reconnect", "success")),
            Rule(
                "linux.sshd.login_failed",
                "Linux sshd failed password",
                "sshd Failed password line — user + source IP via regex.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = ["linux-journal", "linux-syslog", "linux-auth"],
                    SourceType = ["endpoint", "linux"],
                    MessagePatterns = [new SecEventParseRuleMessagePattern { Family = "sshd_failed_password" }]
                },
                extract:
                [
                    Regex(
                        @"Failed password for (?:invalid user )?(?<user>\S+) from (?<ip>[\d.]+)",
                        ("user", "actor.user"),
                        ("ip", "network.srcIp")),
                    C("event.action", "login_failed"),
                    C("event.outcome", "failure"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "linux.sshd.login_success",
                "Linux sshd accepted password",
                "sshd Accepted password line — user + source IP via regex.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = ["linux-journal", "linux-syslog", "linux-auth"],
                    SourceType = ["endpoint", "linux"],
                    MessagePatterns = [new SecEventParseRuleMessagePattern { Family = "sshd_accepted" }]
                },
                extract:
                [
                    Regex(
                        @"Accepted password for (?<user>\S+) from (?<ip>[\d.]+)",
                        ("user", "actor.user"),
                        ("ip", "network.srcIp")),
                    C("event.action", "login_success"),
                    C("event.outcome", "success"),
                    C("event.category", "authentication")
                ]),
            Rule(
                "linux.sudo.not_allowed",
                "Linux sudo command not allowed",
                "sudo command not allowed — extracts invoking user.",
                priority: 100,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = ["linux-journal", "linux-syslog", "linux-auth"],
                    SourceType = ["endpoint", "linux"],
                    MessagePatterns = [new SecEventParseRuleMessagePattern { Family = "sudo_not_allowed" }]
                },
                extract:
                [
                    Regex(
                        @"sudo:\s+(?<user>\S+)\s+:\s+command not allowed",
                        ("user", "actor.user")),
                    C("event.action", "privilege_denied"),
                    C("event.outcome", "failure"),
                    C("event.category", "authorization")
                ]),
            Rule(
                "linux.sudo.command",
                "Linux sudo command",
                "sudo COMMAND= line — user + optional command text.",
                priority: 90,
                now,
                match: new SecEventParseRuleMatch
                {
                    SourceProduct = ["linux-journal", "linux-syslog", "linux-auth"],
                    SourceType = ["endpoint", "linux"],
                    MessagePatterns = [new SecEventParseRuleMessagePattern { Family = "sudo_command" }]
                },
                extract:
                [
                    Regex(
                        @"sudo:\s+(?<user>\S+)\s+:.*COMMAND=(?<cmd>.+)$",
                        ("user", "actor.user"),
                        ("cmd", "custom.sudo_command")),
                    C("event.action", "privilege_escalation"),
                    C("event.outcome", "success"),
                    C("event.category", "authorization")
                ])
        ];
    }

    private static List<SecEventParseRuleExtractStep> GroupMemberExtract() =>
    [
        Ed("SubjectUserName", "actor.user"),
        Ed("MemberName", "custom.member"),
        Ed("TargetUserName", "custom.group"),
        Ed("TargetDomainName", "custom.target_domain"),
        C("event.action", "group_member_added"),
        C("event.outcome", "success"),
        C("event.category", "authorization")
    ];

    private static List<SecEventParseRuleExtractStep> DirectoryObjectExtract(string action) =>
    [
        Ed("SubjectUserName", "actor.user"),
        Ed("ObjectDN", "custom.object_dn"),
        Ed("AttributeLDAPDisplayName", "custom.attribute"),
        Ed("OpCorrelationID", "custom.correlation_id"),
        C("event.action", action),
        C("event.outcome", "success"),
        C("event.category", "config_change")
    ];

    private static List<SecEventParseRuleExtractStep> RdpExtract(string action, string outcome) =>
    [
        // Message-line harvest first; EventData overwrites when present (last write wins).
        Regex(
            @"(?im)^\s*User:\s*(?<user>.+?)\s*$",
            ("user", "actor.user")),
        Regex(
            @"(?im)^\s*Source Network Address:\s*(?<ip>\S+)\s*$",
            ("ip", "network.srcIp")),
        Regex(
            @"(?im)^\s*Session ID:\s*(?<sid>\S+)\s*$",
            ("sid", "custom.session_id")),
        Ed("User", "actor.user"),
        Ed("Address", "network.srcIp"),
        Ed("SessionID", "custom.session_id"),
        C("event.action", action),
        C("event.outcome", outcome),
        C("event.category", "authentication")
    ];

    private static SecEventParseRuleDocument Rule(
        string ruleId,
        string name,
        string description,
        int priority,
        DateTime now,
        SecEventParseRuleMatch match,
        List<SecEventParseRuleExtractStep> extract) =>
        new()
        {
            RuleId = ruleId,
            Name = name,
            Description = description,
            Enabled = true,
            Priority = priority,
            Builtin = true,
            Version = SeedRevision,
            Match = match,
            Extract = extract,
            OnConflict = "first_wins",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static SecEventParseRuleExtractStep Ed(string from, string to) => new()
    {
        Type = "event_data",
        From = from,
        To = to
    };

    private static SecEventParseRuleExtractStep C(string to, string value) => new()
    {
        Type = "constant",
        To = to,
        Value = value
    };

    private static SecEventParseRuleExtractStep Regex(
        string pattern,
        params (string group, string to)[] groups) =>
        new()
        {
            Type = "regex",
            From = "message",
            Pattern = pattern,
            Groups = groups.ToDictionary(g => g.group, g => g.to, StringComparer.Ordinal)
        };
}
