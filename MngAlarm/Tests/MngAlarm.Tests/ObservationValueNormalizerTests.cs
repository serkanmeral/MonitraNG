using System.Text.Json;
using MngAlarm.Application.Observations;

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
}
