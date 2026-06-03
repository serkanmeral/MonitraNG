using MngWorkflow.Application.Configuration;
using MngWorkflow.Infrastructure.Nodes;
using Xunit;

namespace MngWorkflow.Tests;

public sealed class EngineCommandNodeTests
{
    [Fact]
    public void BuildCommandTopic_WithoutPrefix()
    {
        var settings = new EngineCommandSettings();
        var topic = EngineCommandNode.BuildCommandTopic(settings, "odak", "eng-1");
        Assert.Equal("monitoring/odak/engine/eng-1/command", topic);
    }

    [Fact]
    public void BuildCommandTopic_WithPrefix()
    {
        var settings = new EngineCommandSettings { MqttTopicPrefix = "MNG" };
        var topic = EngineCommandNode.BuildCommandTopic(settings, "odak", "eng-1");
        Assert.Equal("MNG/monitoring/odak/engine/eng-1/command", topic);
    }
}
