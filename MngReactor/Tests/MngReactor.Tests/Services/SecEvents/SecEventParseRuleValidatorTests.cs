using MngReactor.Application.Contracts.SecEvents;
using MngReactor.Application.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventParseRuleValidatorTests
{
    [Fact]
    public void ValidateUpsert_AcceptsWindowsLogonRule()
    {
        var request = ValidWindows4625();
        SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true);
    }

    [Fact]
    public void ValidateUpsert_RejectsUnknownTargetField()
    {
        var request = ValidWindows4625();
        request.Extract.Add(new SecEventParseRuleExtractStepDto
        {
            Type = "constant",
            To = "threat.technique.id",
            Value = "T1110.001"
        });

        var ex = Assert.Throws<ArgumentException>(() =>
            SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true));
        Assert.Contains("not allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateUpsert_AcceptsCustomTargetField()
    {
        var request = ValidWindows4625();
        request.Extract.Add(new SecEventParseRuleExtractStepDto
        {
            Type = "regex",
            Pattern = @"SessionID:\s*(?<sid>\d+)",
            Groups = new Dictionary<string, string> { ["sid"] = "session_id" }
        });

        SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true);
        Assert.Equal("custom.session_id", request.Extract[^1].Groups!["sid"]);
    }

    [Fact]
    public void ValidateUpsert_AcceptsCustomDottedTarget()
    {
        var request = ValidWindows4625();
        request.Extract.Add(new SecEventParseRuleExtractStepDto
        {
            Type = "constant",
            To = "custom.workstation",
            Value = "WS1"
        });

        SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true);
        Assert.Equal("custom.workstation", request.Extract[^1].To);
    }

    [Fact]
    public void ValidateUpsert_RejectsUnknownExtractType()
    {
        var request = ValidWindows4625();
        request.Extract =
        [
            new SecEventParseRuleExtractStepDto { Type = "grok", To = "actor.user", From = "x" }
        ];

        var ex = Assert.Throws<ArgumentException>(() =>
            SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true));
        Assert.Contains("not allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateUpsert_RejectsUnknownMessageFamily()
    {
        var request = ValidWindows4625();
        request.Match.MessagePatterns =
        [
            new SecEventParseRuleMessagePatternDto { Family = "not_a_real_family" }
        ];

        var ex = Assert.Throws<ArgumentException>(() =>
            SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true));
        Assert.Contains("whitelist", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateUpsert_RequiresSourceProduct()
    {
        var request = ValidWindows4625();
        request.Match.SourceProduct = [];

        Assert.Throws<ArgumentException>(() =>
            SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true));
    }

    private static SecEventParseRuleUpsertRequest ValidWindows4625() => new()
    {
        RuleId = "windows.logon.4625",
        Name = "Windows failed logon",
        Enabled = true,
        Priority = 100,
        Match = new SecEventParseRuleMatchDto
        {
            SourceProduct = ["windows"],
            EventIds = [4625]
        },
        Extract =
        [
            new SecEventParseRuleExtractStepDto
            {
                Type = "event_data",
                From = "TargetUserName",
                To = "actor.user"
            },
            new SecEventParseRuleExtractStepDto
            {
                Type = "constant",
                To = "event.action",
                Value = "login_failed"
            }
        ]
    };
}
