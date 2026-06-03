using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Infrastructure.Expressions;
using Xunit;

namespace MngWorkflow.Tests.Expressions;

public sealed class JintWorkflowExpressionEvaluatorTests
{
    private readonly JintWorkflowExpressionEvaluator _evaluator = new(
        Options.Create(new MngWorkflowSettings()),
        NullLogger<JintWorkflowExpressionEvaluator>.Instance);

    [Theory]
    [InlineData("event.value > 5", 10, true)]
    [InlineData("event.value > 5", 3, false)]
    [InlineData("event.country != \"TR\"", "US", true)]
    [InlineData("variables.flag === true", true, true)]
    public void Evaluates_boolean_expressions(string expression, object value, bool expected)
    {
        var context = expression.StartsWith("variables.", StringComparison.Ordinal)
            ? BuildContext(variables: new Dictionary<string, object?> { ["flag"] = value })
            : expression.StartsWith("event.country", StringComparison.Ordinal)
                ? BuildContext(eventData: new Dictionary<string, object?> { ["country"] = value })
                : BuildContext(eventData: new Dictionary<string, object?> { ["value"] = value });

        var result = _evaluator.EvaluateBoolean(expression, context);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Rejects_empty_expression()
    {
        var context = BuildContext();
        Assert.Throws<ArgumentException>(() => _evaluator.EvaluateBoolean("  ", context));
    }

    private static WorkflowExecutionContext BuildContext(
        Dictionary<string, object?>? eventData = null,
        Dictionary<string, object?>? variables = null) =>
        new()
        {
            InstanceId = "i1",
            WorkflowVersionId = "v1",
            DomainId = "d1",
            DomainName = "odak",
            CorrelationId = "c1",
            Event = eventData ?? new Dictionary<string, object?>(),
            Variables = variables ?? new Dictionary<string, object?>(),
            Outputs = new Dictionary<string, object?>()
        };
}
