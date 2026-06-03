using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Engine;
using Xunit;

namespace MngWorkflow.Tests;

public sealed class ParallelJoinBarrierTests
{
    [Fact]
 public void TryRegisterArrival_waits_until_all_inbound_edges_arrive()
    {
        var instance = new WorkflowInstanceDocument { ExecutionContext = new Dictionary<string, object?>() };
        var version = new WorkflowVersionDocument
        {
            Edges =
            [
                new WorkflowEdgeDefinition { FromNodeId = "log_a", ToNodeId = "join_1", EdgeKey = "default" },
                new WorkflowEdgeDefinition { FromNodeId = "log_b", ToNodeId = "join_1", EdgeKey = "default" }
            ]
        };

        Assert.False(ParallelJoinBarrier.TryRegisterArrival(instance, version, "join_1", "log_a", out var c1, out var e1));
        Assert.Equal(1, c1);
        Assert.Equal(2, e1);

        Assert.True(ParallelJoinBarrier.TryRegisterArrival(instance, version, "join_1", "log_b", out var c2, out _));
        Assert.Equal(2, c2);
    }

    [Fact]
    public void Clear_removes_join_state()
    {
        var instance = new WorkflowInstanceDocument { ExecutionContext = new Dictionary<string, object?>() };
        var version = new WorkflowVersionDocument
        {
            Edges =
            [
                new WorkflowEdgeDefinition { FromNodeId = "log_a", ToNodeId = "join_1", EdgeKey = "default" }
            ]
        };

        ParallelJoinBarrier.TryRegisterArrival(instance, version, "join_1", "log_a", out _, out _);
        ParallelJoinBarrier.Clear(instance, "join_1");

        Assert.True(ParallelJoinBarrier.TryRegisterArrival(instance, version, "join_1", "log_a", out var c, out _));
        Assert.Equal(1, c);
    }
}
