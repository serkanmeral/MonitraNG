using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Nodes;

public sealed class ManualTriggerNode : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.ManualTrigger;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken) =>
        Task.FromResult(NodeExecutionResult.Ok(output: new Dictionary<string, object?>
        {
            ["triggerType"] = "manual",
            ["receivedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }));
}

public sealed class IfNode(IWorkflowExpressionEvaluator expressionEvaluator) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.If;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        if (node.Config.TryGetValue("expression", out var exprRaw) &&
            !string.IsNullOrWhiteSpace(exprRaw?.ToString()))
        {
            var exprMatched = expressionEvaluator.EvaluateBoolean(exprRaw.ToString()!, context);
            return Task.FromResult(NodeExecutionResult.Ok(nextEdges: [exprMatched ? "true" : "false"], output: new Dictionary<string, object?>
            {
                ["matched"] = exprMatched,
                ["mode"] = "expression"
            }));
        }

        var field = node.Config.TryGetValue("field", out var f) ? f?.ToString() : "event.value";
        var op = node.Config.TryGetValue("operator", out var o) ? o?.ToString() : "eq";
        node.Config.TryGetValue("value", out var compareRaw);

        var left = ContextPathReader.Read(context, field);
        var matched = Compare(left, compareRaw, op);
        return Task.FromResult(NodeExecutionResult.Ok(nextEdges: [matched ? "true" : "false"], output: new Dictionary<string, object?>
        {
            ["matched"] = matched,
            ["left"] = left
        }));
    }

    private static bool Compare(object? left, object? right, string? op) =>
        op switch
        {
            "gt" => ToDouble(left) > ToDouble(right),
            "gte" => ToDouble(left) >= ToDouble(right),
            "lt" => ToDouble(left) < ToDouble(right),
            "lte" => ToDouble(left) <= ToDouble(right),
            "neq" => !Equals(Normalize(left), Normalize(right)),
            _ => Equals(Normalize(left), Normalize(right))
        };

    private static double ToDouble(object? value) =>
        value switch
        {
            null => 0,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDouble(),
            _ => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0
        };

    private static object? Normalize(object? value) =>
        value is JsonElement je ? IfNodeJsonHelper.ToObject(je) : value;

    private static object? JsonElementToObject(JsonElement element) => IfNodeJsonHelper.ToObject(element);
}

public sealed class HttpRequestNode(
    IHttpClientFactory httpClientFactory,
    IOptions<MngWorkflowSettings> settings,
    IWorkflowSecretResolver secretResolver) : IWorkflowNode
{
    private readonly EngineSettings _engine = settings.Value.Engine;

    public string NodeType => WorkflowNodeTypes.HttpRequest;

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        var method = node.Config.TryGetValue("method", out var m) ? m?.ToString() ?? "GET" : "GET";
        var urlRaw = node.Config.TryGetValue("url", out var u) ? u?.ToString() : null;
        if (string.IsNullOrWhiteSpace(urlRaw))
            return NodeExecutionResult.Fail("http.request: url is required");

        var url = secretResolver.Resolve(context.DomainName, urlRaw);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_engine.HttpTimeoutSeconds));

        var client = httpClientFactory.CreateClient("workflow-http");
        using var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (node.Config.TryGetValue("headers", out var headersRaw) && headersRaw is Dictionary<string, object?> headers)
        {
            foreach (var (key, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(key) || value == null)
                    continue;
                request.Headers.TryAddWithoutValidation(key, secretResolver.Resolve(context.DomainName, value.ToString()!));
            }
        }

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var body = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var retryable = (int)response.StatusCode >= 500;
            return NodeExecutionResult.Fail(
                $"HTTP {(int)response.StatusCode}: {Trim(body, 500)}",
                retryable: retryable);
        }

        return NodeExecutionResult.Ok(output: new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["bodyPreview"] = Trim(body, 500)
        });
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

public sealed class WriteLogNode(ILogger<WriteLogNode> logger) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.WriteLog;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        var message = node.Config.TryGetValue("message", out var m) ? m?.ToString() : "write.log";
        logger.LogInformation(
            "Workflow log node instance={InstanceId} correlation={CorrelationId} message={Message}",
            context.InstanceId,
            context.CorrelationId,
            message);

        return Task.FromResult(NodeExecutionResult.Ok(output: new Dictionary<string, object?>
        {
            ["message"] = message,
            ["loggedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }));
    }
}

internal static class ContextPathReader
{
    public static object? Read(WorkflowExecutionContext context, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            return null;

        Dictionary<string, object?>? current = segments[0] switch
        {
            "event" => context.Event,
            "variables" => context.Variables,
            "outputs" => context.Outputs,
            _ => null
        };

        if (current == null)
            return null;

        object? value = null;
        for (var i = 1; i < segments.Length; i++)
        {
            if (current == null || !current.TryGetValue(segments[i], out value))
                return null;

            current = value as Dictionary<string, object?>;
            if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
            {
                current = JsonObjectToDictionary(je);
                value = current;
            }
        }

        return value is JsonElement element ? IfNodeJsonHelper.ToObject(element) : value;
    }

    private static Dictionary<string, object?> JsonObjectToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = IfNodeJsonHelper.ToObject(prop.Value);
        return dict;
    }
}

internal static class IfNodeJsonHelper
{
    public static object? ToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToObject(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToList(),
            _ => element.GetRawText()
        };
}
