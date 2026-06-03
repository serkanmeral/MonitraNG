using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowApprovalService : IWorkflowApprovalService
{
    private readonly IWorkflowDomainAccessor _domain;
    private readonly IWorkflowApprovalRepository _approvals;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowResumeService _resume;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WorkflowApprovalService> _logger;

    public WorkflowApprovalService(
        IWorkflowDomainAccessor domain,
        IWorkflowApprovalRepository approvals,
        IWorkflowInstanceRepository instances,
        IWorkflowResumeService resume,
        IConfiguration configuration,
        ILogger<WorkflowApprovalService> logger)
    {
        _domain = domain;
        _approvals = approvals;
        _instances = instances;
        _resume = resume;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WorkflowApprovalSummary>> ListAsync(
        WorkflowApprovalStatus? status,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var items = await _approvals.ListAsync(ctx.DomainName, status, skip, limit, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<WorkflowApprovalSummary?> GetAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var doc = await _approvals.GetByIdAsync(ctx.DomainName, approvalId, cancellationToken);
        return doc == null ? null : Map(doc);
    }

    public async Task<WorkflowApprovalDecisionResult> DecideAsync(
        string approvalId,
        DecideWorkflowApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var approval = await _approvals.GetByIdAsync(ctx.DomainName, approvalId, cancellationToken)
            ?? throw new KeyNotFoundException($"Approval '{approvalId}' not found.");

        if (approval.Status != WorkflowApprovalStatus.Pending)
            throw new InvalidOperationException("Approval is already decided.");

        var decidedBy = request.DecidedBy?.Trim();
        if (string.IsNullOrWhiteSpace(decidedBy))
            throw new ArgumentException("decidedBy is required.");

        if (!IsApproverAuthorized(approval.ApproverTarget, decidedBy))
            throw new UnauthorizedAccessException($"Actor '{decidedBy}' is not authorized for target '{approval.ApproverTarget}'.");

        var instance = await _instances.GetByIdAsync(ctx.DomainName, approval.InstanceId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow instance not found.");

        if (instance.Status != WorkflowInstanceStatus.Waiting)
            throw new InvalidOperationException("Workflow instance is not waiting.");

        if (!instance.CurrentNodes.Contains(approval.NodeId))
            throw new InvalidOperationException("Approval node is not the current waiting node.");

        var edgeKey = request.Approved ? "approved" : "rejected";
        var now = DateTime.UtcNow;

        approval.Status = request.Approved ? WorkflowApprovalStatus.Approved : WorkflowApprovalStatus.Rejected;
        approval.DecidedBy = decidedBy;
        approval.Comment = request.Comment;
        approval.DecidedAt = now;
        await _approvals.UpdateAsync(approval, cancellationToken);

        var result = await _resume.ResumeFromWaitingNodeAsync(
            ctx.DomainName,
            approval.InstanceId,
            approval.NodeId,
            edgeKey,
            new Dictionary<string, object?>
            {
                ["approvalId"] = approval.Id,
                ["decision"] = edgeKey,
                ["decidedBy"] = decidedBy,
                ["decidedAt"] = now.ToString("O")
            },
            cancellationToken);

        _logger.LogInformation(
            "Approval resume instance={InstanceId} approval={ApprovalId} edge={EdgeKey} status={Status}",
            approval.InstanceId, approval.Id, edgeKey, result.InstanceStatus);

        return new WorkflowApprovalDecisionResult(
            approval.Id,
            approval.InstanceId,
            request.Approved,
            edgeKey,
            result.InstanceStatus);
    }

    private bool IsApproverAuthorized(string approverTarget, string decidedBy)
    {
        if (IsDevRelaxed())
            return true;

        return string.Equals(approverTarget, decidedBy, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDevRelaxed() =>
        _configuration.GetValue<bool>("EnableDevWorkflowEndpoints")
        || string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);

    private static WorkflowApprovalSummary Map(WorkflowApprovalDocument doc) =>
        new()
        {
            Id = doc.Id,
            InstanceId = doc.InstanceId,
            WorkflowId = doc.WorkflowId,
            NodeId = doc.NodeId,
            ApproverTarget = doc.ApproverTarget,
            Status = doc.Status,
            DecidedBy = doc.DecidedBy,
            Comment = doc.Comment,
            CreatedAt = doc.CreatedAt,
            DecidedAt = doc.DecidedAt
        };
}
