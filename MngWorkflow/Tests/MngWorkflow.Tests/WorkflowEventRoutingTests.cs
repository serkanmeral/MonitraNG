using MngWorkflow.Infrastructure.Messaging;
using MngWorkflow.Infrastructure.Services;
using MngWorkflow.Domain.Entities;
using Xunit;

namespace MngWorkflow.Tests.Messaging;

public sealed class WorkflowEventRoutingTests
{
    [Fact]
    public void Parses_oc_workitem_routing_key()
    {
        var parsed = WorkflowEventRouting.ParseTopicRoutingKey("odak-dev.oc.workitem.created");
        Assert.NotNull(parsed);
        Assert.Equal("odak-dev", parsed.Value.DomainId);
        Assert.Equal("oc.workitem.created", parsed.Value.EventType);
    }

    [Theory]
    [InlineData("odak.alarm.raised.7", "alarm.raised")]
    [InlineData("odak.alarm.updated.3", "alarm.updated")]
    [InlineData("odak.alarm.resolved.5", "alarm.resolved")]
    [InlineData("odak.oc.workitem.created", "oc.workitem.created")]
    public void Normalizes_alarm_routing_event_type(string routingKey, string expectedEventType)
    {
        var parsed = WorkflowEventRouting.ParseTopicRoutingKey(routingKey);
        Assert.NotNull(parsed);
        Assert.Equal(expectedEventType, parsed.Value.EventType);
    }

    [Fact]
    public void ResolveEventType_reads_config()
    {
        var trigger = new WorkflowTriggerDefinition
        {
            Config = new Dictionary<string, object?> { ["eventType"] = "oc.workitem.created" }
        };

        Assert.Equal("oc.workitem.created", WorkflowTriggerSyncService.ResolveEventType(trigger));
    }
}
