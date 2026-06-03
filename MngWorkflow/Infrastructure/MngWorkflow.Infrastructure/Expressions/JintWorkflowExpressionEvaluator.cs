using System.Text.Json;
using Jint;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Services;
using JintEngine = Jint.Engine;

namespace MngWorkflow.Infrastructure.Expressions;

public sealed class JintWorkflowExpressionEvaluator : IWorkflowExpressionEvaluator
{
    private readonly ExpressionSettings _settings;
    private readonly ILogger<JintWorkflowExpressionEvaluator> _logger;

    public JintWorkflowExpressionEvaluator(
        IOptions<MngWorkflowSettings> settings,
        ILogger<JintWorkflowExpressionEvaluator> logger)
    {
        _settings = settings.Value.Engine.Expression;
        _logger = logger;
    }

    public bool EvaluateBoolean(string expression, WorkflowExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression is required.", nameof(expression));

        try
        {
            var engine = CreateEngine();
            engine.SetValue("event", ToJsValue(engine, context.Event));
            engine.SetValue("variables", ToJsValue(engine, context.Variables));
            engine.SetValue("outputs", ToJsValue(engine, context.Outputs));

            var result = engine.Evaluate(expression).ToObject();
            return result switch
            {
                bool b => b,
                null => false,
                _ => Convert.ToBoolean(result)
            };
        }
        catch (JavaScriptException ex)
        {
            _logger.LogWarning(ex, "Expression evaluation failed: {Expression}", expression);
            throw new InvalidOperationException($"Expression error: {ex.Message}", ex);
        }
    }

    private JintEngine CreateEngine() =>
        new(options =>
        {
            options.TimeoutInterval(TimeSpan.FromMilliseconds(_settings.TimeoutMilliseconds));
            options.MaxStatements(_settings.MaxStatements);
            options.LimitRecursion(_settings.MaxRecursionDepth);
            options.Strict();
        });

    private static object ToJsValue(JintEngine engine, Dictionary<string, object?> source)
    {
        var json = JsonSerializer.Serialize(source);
        return engine.Evaluate($"({json})");
    }
}
