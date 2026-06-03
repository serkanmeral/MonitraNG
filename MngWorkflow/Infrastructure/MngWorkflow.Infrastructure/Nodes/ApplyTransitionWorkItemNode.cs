using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Infrastructure.Clients;

namespace MngWorkflow.Infrastructure.Nodes;

public sealed class ApplyTransitionWorkItemNode(IServiceScopeFactory scopeFactory) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.WorkItemTransition;

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var templates = scope.ServiceProvider.GetRequiredService<IWorkflowContextTemplateResolver>();
        var keeper = scope.ServiceProvider.GetRequiredService<IWorkflowKeeperAuthClient>();
        var operations = scope.ServiceProvider.GetRequiredService<IWorkflowOperationsClient>();

        var workItemId = templates.ResolveOptional(context, context.DomainName, GetString(node, "workItemId"));
        var transitionKey = templates.ResolveOptional(context, context.DomainName, GetString(node, "transitionKey"));

        if (string.IsNullOrWhiteSpace(workItemId))
            return NodeExecutionResult.Fail("workitem.transition: workItemId is required", retryable: false);
        if (string.IsNullOrWhiteSpace(transitionKey))
            return NodeExecutionResult.Fail("workitem.transition: transitionKey is required", retryable: false);

        var token = await keeper.GetServiceAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return NodeExecutionResult.Fail("workitem.transition: service account token unavailable", retryable: false);

        var request = new WorkflowTransitionWorkItemRequest
        {
            Comment = templates.ResolveOptional(context, context.DomainName, GetString(node, "comment")),
            Fields = TryParseFields(node, context, templates)
        };

        try
        {
            var response = await operations.ApplyTransitionAsync(
                token,
                workItemId.Trim(),
                transitionKey.Trim(),
                request,
                cancellationToken);

            return NodeExecutionResult.Ok(output: new Dictionary<string, object?>
            {
                ["workItemId"] = response.WorkItem.Id,
                ["workItemKey"] = response.WorkItem.Key,
                ["stateId"] = response.WorkItem.StateId,
                ["availableTransitions"] = response.AvailableTransitions
                    .Select(t => t.TransitionKey)
                    .ToList()
            });
        }
        catch (WorkflowOperationsException ex)
        {
            return NodeExecutionResult.Fail(ex.Message, retryable: ex.IsRetryable);
        }
    }

    private static string? GetString(WorkflowNodeDefinition node, string key) =>
        node.Config.TryGetValue(key, out var raw) ? raw?.ToString() : null;

    private static JsonElement? TryParseFields(
        WorkflowNodeDefinition node,
        WorkflowExecutionContext context,
        IWorkflowContextTemplateResolver templates)
    {
        if (!node.Config.TryGetValue("fields", out var raw) || raw == null)
            return null;

        if (raw is JsonElement je)
            return je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : je;

        if (raw is Dictionary<string, object?> dict)
        {
            var resolved = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, value) in dict)
            {
                resolved[key] = value is string s
                    ? templates.Resolve(context, context.DomainName, s)
                    : value;
            }

            return JsonSerializer.SerializeToElement(resolved);
        }

        return null;
    }
}
