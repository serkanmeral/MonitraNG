using System.Text.Json;
using MngAlarm.Application.Observations;
using Xunit;

namespace MngAlarm.Tests;

public sealed class EventObservationMapperTests
{
    [Fact]
    public void TryMap_SecEventPayload_ParsesNestedDimensions()
    {
        const string json = """
            {
              "domainName": "odak",
              "domainId": "odak",
              "kind": "event",
              "key": "login_failed",
              "value": 1,
              "timestamp": "2026-06-03T14:00:02Z",
              "dimensions": {
                "userId": "admin",
                "srcIp": "192.168.1.50"
              }
            }
            """;

        var envelope = EventObservationMapper.TryMap(JsonDocument.Parse(json).RootElement);

        Assert.NotNull(envelope);
        Assert.Equal("event", envelope!.Kind);
        Assert.Equal("login_failed", envelope.Key);
        Assert.Equal("admin", envelope.Dimensions["userId"]);
        Assert.Equal("192.168.1.50", envelope.Dimensions["srcIp"]);
    }
}
