using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Application.Services.Policy;

public static class DlpPolicySeed
{
    public static DlpPolicyResponse CreateDefault() => new()
    {
        SchemaVersion = 1,
        PolicyId = "odak-default",
        Version = "0",
        PublishedUtc = DateTime.UnixEpoch,
        EnforcementMode = "auditOnly",
        Unclassified = new DlpUnclassifiedPolicy { Allow = true, Effect = "audit" },
        Classifications =
        [
            new DlpClassificationDto { Id = "cl-dahili", Name = "dahili", Sensitivity = 1, PersistToFile = true },
            new DlpClassificationDto { Id = "cl-gizli", Name = "gizli", Sensitivity = 3, PersistToFile = true }
        ],
        Dictionaries = new DlpDictionariesDto
        {
            InternalEmailDomains = ["odak.local", "odak.com.tr", "dlp.internal"],
            SanctionedProcesses = ["OUTLOOK.EXE", "WINWORD.EXE", "EXCEL.EXE", "EXPLORER.EXE"],
            UnsanctionedProcesses = ["WhatsApp.exe", "chrome.exe", "msedge.exe"]
        },
        Rules =
        [
            new DlpRuleDto
            {
                Id = "r-gizli-email-external-block",
                Name = "Gizli - dış e-posta",
                Enabled = true,
                Priority = 100,
                ClassificationIds = ["cl-gizli"],
                Actions = ["email.send"],
                Destination = new DlpDestinationDto { EmailScope = "external" },
                ExceptGroupIds = [],
                Effect = "block"
            },
            new DlpRuleDto
            {
                Id = "r-gizli-email-internal-audit",
                Name = "Gizli - iç e-posta",
                Enabled = true,
                Priority = 200,
                ClassificationIds = ["cl-gizli"],
                Actions = ["email.send"],
                Destination = new DlpDestinationDto { EmailScope = "internal" },
                ExceptGroupIds = [],
                Effect = "audit"
            },
            new DlpRuleDto
            {
                Id = "r-any-email-audit",
                Name = "Diğer e-posta (catch-all)",
                Enabled = true,
                Priority = 900,
                ClassificationIds = ["*"],
                Actions = ["email.send"],
                Destination = new DlpDestinationDto { EmailScope = "any" },
                ExceptGroupIds = [],
                Effect = "audit"
            }
        ]
    };
}
