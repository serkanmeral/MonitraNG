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

public sealed class UpdateWorkItemNode(IServiceScopeFactory scopeFactory) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.WorkItemUpdate;

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
        if (string.IsNullOrWhiteSpace(workItemId))
            return NodeExecutionResult.Fail("workitem.update: workItemId is required", retryable: false);

        var request = new WorkflowPatchWorkItemRequest
        {
            Title = templates.ResolveOptional(context, context.DomainName, GetString(node, "title")),
            Description = ResolveJsonElement(node, context, templates, "description"),
            Assignee = ResolveJsonElement(node, context, templates, "assignee"),
            PriorityId = ResolveJsonElement(node, context, templates, "priorityId"),
            BoardId = ResolveJsonElement(node, context, templates, "boardId"),
            Fields = TryParseFields(node, context, templates)
        };

        if (request.Title == null &&
            request.Description == null &&
            request.Assignee == null &&
            request.PriorityId == null &&
            request.BoardId == null &&
            request.Fields == null)
        {
            return NodeExecutionResult.Fail("workitem.update: at least one patch field is required", retryable: false);
        }

        var token = await keeper.GetServiceAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return NodeExecutionResult.Fail("workitem.update: service account token unavailable", retryable: false);

        try
        {
            var response = await operations.PatchWorkItemAsync(
                token,
                workItemId.Trim(),
                request,
                cancellationToken);

            return NodeExecutionResult.Ok(output: new Dictionary<string, object?>
            {
                ["workItemId"] = response.Id,
                ["workItemKey"] = response.Key,
                ["stateId"] = response.StateId,
                ["title"] = response.Title
            });
        }
        catch (WorkflowOperationsException ex)
        {
            return NodeExecutionResult.Fail(ex.Message, retryable: ex.IsRetryable);
        }
    }

    private static string? GetString(WorkflowNodeDefinition node, string key) =>
        node.Config.TryGetValue(key, out var raw) ? raw?.ToString() : null;

    private static JsonElement? ResolveJsonElement(
        WorkflowNodeDefinition node,
        WorkflowExecutionContext context,
        IWorkflowContextTemplateResolver templates,
        string key)
    {
        if (!node.Config.TryGetValue(key, out var raw) || raw == null)
            return null;

        if (raw is JsonElement je)
            return je.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : je;

        if (raw is string s)
        {
            var resolved = templates.ResolveOptional(context, context.DomainName, s);
            return resolved == null ? null : JsonSerializer.SerializeToElement(resolved);
        }

        return JsonSerializer.SerializeToElement(raw);
    }

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
