using MngLogs.OutlookAddin;

namespace MngLogs.Tests;

public class DlpSendGateTests
{
    [Fact]
    public void Transport_failure_is_fail_open()
    {
        var d = DlpSendGate.FromEvaluate(null, transportFailed: true, "Connection refused");
        Assert.False(d.CancelSend);
        Assert.True(d.FailOpen);
        Assert.Contains("fail-open", d.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dilim1_wouldBlock_still_allows_send()
    {
        var d = DlpSendGate.FromEvaluate(
            new DlpEvaluateDto
            {
                AllowSend = true,
                WouldBlock = true,
                MatchedRuleId = "r-gizli-email-external-block",
                MatchedRuleName = "Gizli - dis e-posta"
            },
            transportFailed: false,
            transportError: null);
        Assert.False(d.CancelSend);
        Assert.True(d.ShowAuditHint);
        Assert.Contains("auditOnly", d.UserMessage);
    }

    [Fact]
    public void AllowSend_false_cancels()
    {
        var d = DlpSendGate.FromEvaluate(
            new DlpEvaluateDto { AllowSend = false, Message = "blocked" },
            transportFailed: false,
            transportError: null);
        Assert.True(d.CancelSend);
        Assert.Equal("blocked", d.UserMessage);
    }

    [Theory]
    [InlineData("SMTP:dis@gmail.com", "dis@gmail.com")]
    [InlineData(" ali@odakkompozit.com.tr ", "ali@odakkompozit.com.tr")]
    [InlineData("EX:/O=ODAK/CN=ALI", null)]
    [InlineData("", null)]
    public void NormalizeSmtp(string raw, string? expected)
    {
        Assert.Equal(expected, AddressUtil.NormalizeSmtp(raw));
    }
}
