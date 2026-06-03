using System.Text;
using MngAlarm.Application.Observations;

namespace MngAlarm.Tests;

public sealed class ObservationIngressParserTests
{
    [Fact]
    public void TryParse_flat_reactor_payload()
    {
        const string json = """
            {
              "domainName": "odak",
              "domainId": "odak",
              "collectibleCode": "cpu_usage",
              "value": 97
            }
            """;

        var envelope = ObservationIngressParser.TryParse(Encoding.UTF8.GetBytes(json));

        Assert.NotNull(envelope);
        Assert.Equal("cpu_usage", envelope!.Key);
        Assert.Equal(97, envelope.Value);
    }

    [Fact]
    public void TryParse_envelope_json_from_bridge()
    {
        const string json = """
            {
              "domainName": "odak",
              "domainId": "odak",
              "kind": "metric",
              "key": "cpu_usage",
              "value": 96
            }
            """;

        var envelope = ObservationIngressParser.TryParse(Encoding.UTF8.GetBytes(json));

        Assert.NotNull(envelope);
        Assert.Equal("cpu_usage", envelope!.Key);
        Assert.Equal(96, envelope.Value);
    }
}
