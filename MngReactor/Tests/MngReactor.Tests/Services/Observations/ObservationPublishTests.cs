using System.Text.Json;
using MngReactor.Application.Observations;
using MngReactor.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Configuration;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.Observations;

public sealed class ObservationPublishMessageTests
{
    [Fact]
    public void SerializeFlatPayload_IncludesRequiredFields()
    {
        var json = ObservationPublishMessage.SerializeFlatPayload(
            "abc",
            "odak",
            "cpu_usage",
            95.5,
            new Dictionary<string, string?> { ["assetId"] = "asset-1" },
            new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("odak", root.GetProperty("domainName").GetString());
        Assert.Equal("abc", root.GetProperty("domainId").GetString());
        Assert.Equal("cpu_usage", root.GetProperty("collectibleCode").GetString());
        Assert.Equal(95.5, root.GetProperty("value").GetDouble());
        Assert.Equal("asset-1", root.GetProperty("assetId").GetString());
        Assert.Equal("2026-06-03T10:00:00.0000000Z", root.GetProperty("timestamp").GetString());
    }

    [Fact]
    public void BuildRoutingKey_MatchesAlarmContract()
    {
        Assert.Equal("abc.metric.cpu_usage", ObservationPublishMessage.BuildRoutingKey("abc", "cpu_usage"));
    }

    [Fact]
    public void SerializeNestedMetaPayload_IncludesMetaCollectibleCode()
    {
        var json = ObservationPublishMessage.SerializeNestedMetaPayload(
            "odak",
            88,
            "cpu_usage",
            new Dictionary<string, string?> { ["assetId"] = "a1" });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("odak", root.GetProperty("domainName").GetString());
        Assert.Equal(88, root.GetProperty("value").GetDouble());
        Assert.Equal("cpu_usage", root.GetProperty("meta").GetProperty("collectibleCode").GetString());
        Assert.Equal("a1", root.GetProperty("meta").GetProperty("assetId").GetString());
    }
}

public sealed class ObservationPublisherTests
{
    [Fact]
    public async Task PublishAsync_WhenDisabled_DoesNotThrow()
    {
        var options = Options.Create(new MngReactorSettings
        {
            ObservationPublish = new ObservationPublishSettings { Enabled = false },
            RabbitMQ = new RabbitmqSettings { Host = "invalid-host-should-not-connect" }
        });
        var logger = new Mock<ILogger<ObservationPublisher>>().Object;
        var sut = new ObservationPublisher(logger, options);

        await sut.PublishAsync("odak", "odak", "cpu_usage", 97);
    }
}
