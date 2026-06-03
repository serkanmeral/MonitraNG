using Microsoft.Extensions.DependencyInjection;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Nodes;

public sealed class ApprovalWaitNode(IServiceScopeFactory scopeFactory) : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.ApprovalWait;

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        var approverTarget = ResolveApproverTarget(node);
        if (string.IsNullOrWhiteSpace(approverTarget))
            return NodeExecutionResult.Fail("approval.wait: approverTarget or approverGroup is required", retryable: false);

        using var scope = scopeFactory.CreateScope();
        var instances = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceRepository>();
        var approvals = scope.ServiceProvider.GetRequiredService<IWorkflowApprovalRepository>();

        var instance = await instances.GetByIdAsync(context.DomainName, context.InstanceId, cancellationToken);
        if (instance == null)
            return NodeExecutionResult.Fail("Instance not found", retryable: false);

        var existing = await approvals.GetPendingByInstanceNodeAsync(
            context.DomainName, context.InstanceId, node.Id, cancellationToken);
        if (existing != null)
        {
            return NodeExecutionResult.Wait(WorkflowWaitingTypes.Approval, new Dictionary<string, object?>
            {
                ["approvalId"] = existing.Id,
                ["approverTarget"] = existing.ApproverTarget,
                ["status"] = "pending"
            });
        }

        var approval = new WorkflowApprovalDocument
        {
            InstanceId = context.InstanceId,
            WorkflowId = instance.WorkflowId,
            WorkflowVersionId = context.WorkflowVersionId,
            DomainId = context.DomainId,
            DomainName = context.DomainName,
            NodeId = node.Id,
            ApproverTarget = approverTarget
        };

        await approvals.InsertAsync(approval, cancellationToken);

        return NodeExecutionResult.Wait(WorkflowWaitingTypes.Approval, new Dictionary<string, object?>
        {
            ["approvalId"] = approval.Id,
            ["approverTarget"] = approverTarget,
            ["status"] = "pending"
        });
    }

    private static string? ResolveApproverTarget(WorkflowNodeDefinition node)
    {
        if (node.Config.TryGetValue("approverTarget", out var target) && !string.IsNullOrWhiteSpace(target?.ToString()))
            return target.ToString()!.Trim();

        if (node.Config.TryGetValue("approverGroup", out var group) && !string.IsNullOrWhiteSpace(group?.ToString()))
            return group.ToString()!.Trim();

        return null;
    }
}
