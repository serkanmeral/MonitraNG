using System.Text.Json;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;

namespace MngAlarm.Tests;

public sealed class ObservationValueNormalizerTests
{
    [Fact]
    public void NormalizeDimensions_converts_json_elements_to_primitives()
    {
        using var doc = JsonDocument.Parse("""{"userId":"u1","srcIp":"10.0.0.1","count":3,"active":true}""");
        var raw = new Dictionary<string, object?>
        {
            ["userId"] = doc.RootElement.GetProperty("userId"),
            ["srcIp"] = doc.RootElement.GetProperty("srcIp"),
            ["count"] = doc.RootElement.GetProperty("count"),
            ["active"] = doc.RootElement.GetProperty("active")
        };

        var normalized = ObservationValueNormalizer.NormalizeDimensions(raw);

        Assert.Equal("u1", normalized["userId"]);
        Assert.Equal("10.0.0.1", normalized["srcIp"]);
        Assert.Equal(3L, normalized["count"]);
        Assert.Equal(true, normalized["active"]);
        Assert.IsNotType<JsonElement>(normalized["userId"]);
    }

    [Fact]
    public void Normalize_preserves_json_array_as_list()
    {
        using var doc = JsonDocument.Parse("""["21","23","24","25"]""");
        var normalized = ObservationValueNormalizer.Normalize(doc.RootElement);

        var list = Assert.IsType<List<object?>>(normalized);
        Assert.Equal(["21", "23", "24", "25"], list.Select(x => x?.ToString()));
    }

    [Fact]
    public void EvaluateCondition_in_matches_event_code_against_list()
    {
        var observation = new ObservationEnvelope
        {
            DomainName = "odak",
            Kind = "event",
            Key = "rdp.reconnect",
            Value = 1,
            Dimensions = new Dictionary<string, object?> { ["eventCode"] = "25" }
        };
        var condition = new ScenarioCondition
        {
            Field = "dimensions.eventCode",
            Operator = "in",
            Value = new List<object?> { "21", "23", "24", "25" }
        };

        Assert.True(ScenarioCompiler.Matches(condition, observation, out _));
    }

    [Fact]
    public void EvaluateCondition_in_matches_legacy_json_array_string()
    {
        var observation = new ObservationEnvelope
        {
            DomainName = "odak",
            Kind = "event",
            Key = "rdp.disconnect",
            Value = 1,
            Dimensions = new Dictionary<string, object?> { ["eventCode"] = "24" }
        };
        var condition = new ScenarioCondition
        {
            Field = "dimensions.eventCode",
            Operator = "in",
            Value = """["21","23","24","25"]"""
        };

        Assert.True(ScenarioCompiler.Matches(condition, observation, out _));
    }
}
