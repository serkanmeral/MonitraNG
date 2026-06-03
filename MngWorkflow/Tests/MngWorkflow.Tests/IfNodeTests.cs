using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Expressions;
using MngWorkflow.Infrastructure.Nodes;
using Xunit;

namespace MngWorkflow.Tests.Nodes;

public sealed class IfNodeTests
{
    private readonly IfNode _node = new(
        new JintWorkflowExpressionEvaluator(
            Options.Create(new MngWorkflowSettings()),
            NullLogger<JintWorkflowExpressionEvaluator>.Instance));

    [Theory]
    [InlineData(10, "true")]
    [InlineData(3, "false")]
    public async Task Evaluates_numeric_gt(int value, string expectedEdge)
    {
        var context = BuildContext(value);
        var def = new WorkflowNodeDefinition
        {
            Id = "if_1",
            Type = "if",
            Config = new Dictionary<string, object?>
            {
                ["field"] = "event.value",
                ["operator"] = "gt",
                ["value"] = 5
            }
        };

        var result = await _node.ExecuteAsync(context, def, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedEdge, result.NextEdges[0]);
    }

    [Theory]
    [InlineData(10, "true")]
    [InlineData(2, "false")]
    public async Task Evaluates_jint_expression(int value, string expectedEdge)
    {
        var context = BuildContext(value);
        var def = new WorkflowNodeDefinition
        {
            Id = "if_1",
            Type = "if",
            Config = new Dictionary<string, object?> { ["expression"] = "event.value > 5" }
        };

        var result = await _node.ExecuteAsync(context, def, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedEdge, result.NextEdges[0]);
    }

    private static WorkflowExecutionContext BuildContext(int value) =>
        new()
        {
            InstanceId = "i1",
            WorkflowVersionId = "v1",
            DomainId = "d1",
            DomainName = "odak",
            CorrelationId = "c1",
            Event = new Dictionary<string, object?> { ["value"] = value },
            Variables = new Dictionary<string, object?>(),
            Outputs = new Dictionary<string, object?>()
        };
}
