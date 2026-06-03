using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Validation;
using Xunit;

namespace MngWorkflow.Tests.Validation;

public sealed class WorkflowGraphValidatorTests
{
    [Fact]
    public void Validates_minimal_graph()
    {
        var nodes = new List<WorkflowNodeDefinition>
        {
            new() { Id = "n1", Type = "manual.trigger" },
            new() { Id = "n2", Type = "write.log" }
        };
        var edges = new List<WorkflowEdgeDefinition>
        {
            new() { FromNodeId = "n1", ToNodeId = "n2", EdgeKey = "default" }
        };

        WorkflowGraphValidator.Validate("n1", nodes, edges);
    }

    [Fact]
    public void Rejects_missing_entry_node()
    {
        var nodes = new List<WorkflowNodeDefinition> { new() { Id = "n1", Type = "manual.trigger" } };
        Assert.Throws<ArgumentException>(() => WorkflowGraphValidator.Validate("missing", nodes, []));
    }
}
