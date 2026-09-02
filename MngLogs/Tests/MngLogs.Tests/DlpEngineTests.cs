using MngLogs.Agent.Dlp;

namespace MngLogs.Tests;

public class DlpEngineTests
{
    private static DlpCompiledPolicy Sample() => new()
    {
        Version = "3",
        EnforcementMode = "auditOnly",
        Unclassified = new DlpUnclassified { Allow = true, Effect = "audit" },
        Classifications =
        [
            new DlpClassification { Id = "cl-gizli", Name = "gizli", Sensitivity = 3 }
        ],
        Dictionaries = new DlpDictionaries { InternalEmailDomains = ["dlp.internal", "odak.local"] },
        Rules =
        [
            new DlpRule
            {
                Id = "r-gizli-email-external-block",
                Name = "Gizli - dış e-posta",
                Enabled = true,
                Priority = 100,
                ClassificationIds = ["cl-gizli"],
                Actions = ["email.send"],
                Destination = new DlpDestination { EmailScope = "external" },
                Effect = "block"
            },
            new DlpRule
            {
                Id = "r-any-email-audit",
                Name = "catch-all",
                Enabled = true,
                Priority = 900,
                ClassificationIds = ["*"],
                Actions = ["email.send"],
                Destination = new DlpDestination { EmailScope = "any" },
                Effect = "audit"
            }
        ]
    };

    [Fact]
    public void AuditOnly_block_rule_allows_send_with_wouldBlock()
    {
        var result = DlpEngine.Evaluate(
            Sample(),
            new DlpEvaluateRequest
            {
                Action = "email.send",
                WindowsUser = @"ODAK\ali",
                Recipients = ["dis@gmail.com"]
            },
            [new DlpClassificationHit { Id = "cl-gizli", Name = "gizli", Sensitivity = 3, Source = "override" }]);

        Assert.Equal("allow", result.Decision);
        Assert.True(result.AllowSend);
        Assert.True(result.WouldBlock);
        Assert.Equal("block", result.Effect);
        Assert.Equal("external", result.EmailScope);
        Assert.Equal("r-gizli-email-external-block", result.MatchedRuleId);
    }

    [Fact]
    public void Unclassified_skips_rules()
    {
        var result = DlpEngine.Evaluate(
            Sample(),
            new DlpEvaluateRequest { Recipients = ["dis@gmail.com"] },
            []);

        Assert.True(result.AllowSend);
        Assert.False(result.WouldBlock);
        Assert.Equal("audit", result.Effect);
        Assert.Null(result.MatchedRuleId);
        Assert.Equal("none", result.Classification?.Source);
    }

    [Fact]
    public void Internal_scope_does_not_match_external_rule()
    {
        var result = DlpEngine.Evaluate(
            Sample(),
            new DlpEvaluateRequest { Recipients = ["ali@dlp.internal"] },
            [new DlpClassificationHit { Id = "cl-gizli", Name = "gizli", Sensitivity = 3, Source = "override" }]);

        Assert.Equal("r-any-email-audit", result.MatchedRuleId);
        Assert.Equal("internal", result.EmailScope);
        Assert.False(result.WouldBlock);
    }

    [Fact]
    public void Enforce_mode_blocks_send()
    {
        var policy = Sample();
        policy.EnforcementMode = "enforce";
        var result = DlpEngine.Evaluate(
            policy,
            new DlpEvaluateRequest { Recipients = ["dis@gmail.com"] },
            [new DlpClassificationHit { Id = "cl-gizli", Sensitivity = 3, Source = "override" }]);

        Assert.Equal("block", result.Decision);
        Assert.False(result.AllowSend);
        Assert.True(result.WouldBlock);
    }
}
