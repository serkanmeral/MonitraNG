using System.Text.Json;
using MngAlarm.Application.Observations;

namespace MngAlarm.Tests;

public sealed class MetricObservationMapperTests
{
    [Fact]
    public void TryMap_FlatReactorPayload_ReturnsEnvelope()
    {
        const string json = """
            {
              "domainName": "odak",
              "domainId": "d-123",
              "collectibleCode": "cpu_usage",
              "value": 95.5,
              "assetId": "asset-1",
              "engineId": "engine-1",
              "timestamp": "2026-06-03T10:00:00Z"
            }
            """;

        var envelope = MetricObservationMapper.TryMap(JsonDocument.Parse(json).RootElement);

        Assert.NotNull(envelope);
        Assert.Equal("odak", envelope.DomainName);
        Assert.Equal("d-123", envelope.DomainId);
        Assert.Equal("cpu_usage", envelope.Key);
        Assert.Equal(95.5, envelope.Value);
        Assert.Equal("metric", envelope.Kind);
        Assert.Equal("asset-1", envelope.Dimensions["assetId"]);
    }

    [Fact]
    public void TryMap_NestedMetaPayload_ReturnsEnvelope()
    {
        const string json = """
            {
              "value": 88,
              "meta": {
                "domain": "odak",
                "collectibleCode": "cpu_usage",
                "assetId": "a1"
              }
            }
            """;

        var envelope = MetricObservationMapper.TryMap(JsonDocument.Parse(json).RootElement);

        Assert.NotNull(envelope);
        Assert.Equal("odak", envelope.DomainName);
        Assert.Equal("cpu_usage", envelope.Key);
        Assert.Equal(88, envelope.Value);
    }

    [Fact]
    public void TryMap_MissingKey_ReturnsNull()
    {
        const string json = """{"domainName":"odak","value":1}""";

        Assert.Null(MetricObservationMapper.TryMap(JsonDocument.Parse(json).RootElement));
    }

    [Fact]
    public void BuildRoutingKey_UsesDomainIdAndKey()
    {
        var envelope = new ObservationEnvelope
        {
            DomainId = "abc",
            DomainName = "odak",
            Key = "cpu_usage",
            Value = 1
        };

        Assert.Equal("abc.metric.cpu_usage", MetricObservationMapper.BuildRoutingKey(envelope));
    }
}
