using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Observations;
using Xunit;

namespace MngReactor.Tests.Services.Observations;

public sealed class SecEventObservationMapperTests
{
    [Fact]
    public void ToPayload_WindowsLoginFailed_MapsEventKeyAndDimensions()
    {
        var doc = new SecEventDocument
        {
            Timestamp = DateTime.Parse("2026-06-03T14:00:02Z").ToUniversalTime(),
            IngestedAt = DateTime.UtcNow,
            Domain = "odak",
            Source = new SecEventSourceInfo { Type = "ad", Product = "windows", Host = "dc01" },
            Event = new SecEventEventBlock
            {
                Action = "login_failed",
                Outcome = "failure",
                Code = "4625"
            },
            Actor = new SecEventActorBlock { User = "admin" },
            Network = new SecEventNetworkBlock { SrcIp = "192.168.1.50" },
            Parser = new SecEventParserBlock { Id = "windows.security.v1" },
            Raw = "preview",
            RawPreview = "preview"
        };

        var payload = SecEventObservationMapper.ToPayload(doc, "odak-domain-id", "odak");

        Assert.Equal("event", payload.Kind);
        Assert.Equal("login_failed", payload.Key);
        Assert.Equal("odak-domain-id", payload.DomainId);
        Assert.Equal("admin", payload.Dimensions["userId"]);
        Assert.Equal("192.168.1.50", payload.Dimensions["srcIp"]);
        Assert.Equal("ad", payload.Dimensions["sourceType"]);
    }

    [Fact]
    public void SerializeEventPayload_UsesEventRoutingKeyShape()
    {
        var payload = new SecEventObservationPayload
        {
            DomainId = "abc123",
            DomainName = "odak",
            Kind = "event",
            Key = "login_failed",
            Value = 1,
            Timestamp = DateTime.Parse("2026-06-03T14:00:02Z").ToUniversalTime(),
            Dimensions = new Dictionary<string, object?> { ["userId"] = "admin" }
        };

        var json = ObservationPublishMessage.SerializeEventPayload(payload);
        Assert.Contains("\"kind\":\"event\"", json);
        Assert.Contains("\"key\":\"login_failed\"", json);
        Assert.Equal("abc123.event.login_failed", ObservationPublishMessage.BuildEventRoutingKey("abc123", "login_failed"));
    }

    [Fact]
    public void ToNewFlowPayload_CopiesDimensionsWithNewFlowKey()
    {
        var primary = new SecEventObservationPayload
        {
            DomainId = "abc123",
            DomainName = "odak",
            Kind = "event",
            Key = "denied_flow",
            Value = 1,
            Timestamp = DateTime.UtcNow,
            Dimensions = new Dictionary<string, object?> { ["srcIp"] = "10.0.0.1", ["dstIp"] = "10.0.0.2" }
        };

        var newFlow = SecEventObservationMapper.ToNewFlowPayload(primary);

        Assert.Equal("new_flow", newFlow.Key);
        Assert.Equal("denied_flow", primary.Key);
        Assert.Equal("10.0.0.1", newFlow.Dimensions["srcIp"]);
    }
}
