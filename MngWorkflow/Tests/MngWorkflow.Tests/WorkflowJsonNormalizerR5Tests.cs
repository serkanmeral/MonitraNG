using System.Text.Json;
using MngWorkflow.Infrastructure.Utilities;
using Xunit;

namespace MngWorkflow.Tests.Utilities;

/// <summary>R5 — AI-ready: nested event/alarm context alanları normalize sonrası korunur.</summary>
public sealed class WorkflowJsonNormalizerR5Tests
{
    [Fact]
    public void Preserves_nested_alarm_context_for_ai_input()
    {
        var json = """
            {
              "eventType": "AlarmRaised",
              "severity": 7,
              "context": {
                "key": "auth_failure",
                "groupKey": "u1|10.0.0.1",
                "windowCount": 10,
                "userId": "u1",
                "srcIp": "10.0.0.1"
              }
            }
            """;
        var element = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        var normalized = WorkflowJsonNormalizer.NormalizeDictionary(element);

        Assert.Equal("AlarmRaised", normalized["eventType"]);
        var context = Assert.IsType<Dictionary<string, object?>>(normalized["context"]);
        Assert.Equal("auth_failure", context["key"]);
        Assert.Equal("u1|10.0.0.1", context["groupKey"]);
        Assert.Equal(10, Convert.ToInt32(context["windowCount"]));
        Assert.Equal("u1", context["userId"]);
        Assert.Equal("10.0.0.1", context["srcIp"]);
    }
}
