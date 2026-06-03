using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowHookService : IWorkflowHookService
{
    private readonly IWorkflowResumeService _resume;
    private readonly IWorkflowRunService _runs;
    private readonly IWorkflowVersionRepository _versions;

    public WorkflowHookService(
        IWorkflowResumeService resume,
        IWorkflowRunService runs,
        IWorkflowVersionRepository versions)
    {
        _resume = resume;
        _runs = runs;
        _versions = versions;
    }

    public Task<WorkflowResumeResult> ResumeDelayAsync(WorkflowDelayResumeRequest request, CancellationToken cancellationToken = default)
    {
        var domainName = ResolveDomainName(request.DomainName, request.DomainId);
        var edgeKey = string.IsNullOrWhiteSpace(request.EdgeKey) ? "default" : request.EdgeKey.Trim();

        return _resume.ResumeFromWaitingNodeAsync(
            domainName,
            request.InstanceId.Trim(),
            request.NodeId.Trim(),
            edgeKey,
            new Dictionary<string, object?>
            {
                ["resumedAt"] = DateTime.UtcNow.ToString("O"),
                ["delayCompleted"] = true
            },
            cancellationToken);
    }

    public async Task<WorkflowRunResult> RunScheduleTriggerAsync(
        WorkflowScheduleRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowId))
            throw new ArgumentException("workflowId is required.");

        var domainName = ResolveDomainName(request.DomainName, request.DomainId);
        var domainId = string.IsNullOrWhiteSpace(request.DomainId) ? domainName : request.DomainId.Trim();

        WorkflowVersionDocument version;
        if (!string.IsNullOrWhiteSpace(request.WorkflowVersionId))
        {
            version = await _versions.GetByIdAsync(domainName, request.WorkflowVersionId.Trim(), cancellationToken)
                ?? throw new InvalidOperationException("Published workflow version not found.");
        }
        else
        {
            version = await _versions.GetPublishedByWorkflowIdAsync(domainName, request.WorkflowId.Trim(), cancellationToken)
                ?? throw new InvalidOperationException("Published workflow version not found.");
        }

        if (version.Status != WorkflowVersionStatus.Published)
            throw new InvalidOperationException("Only published versions can be executed.");

        return await _runs.StartEventRunAsync(
            version,
            new Dictionary<string, object?>
            {
                ["triggerType"] = "schedule",
                ["workflowId"] = version.WorkflowId,
                ["scheduledAt"] = DateTime.UtcNow.ToString("O")
            },
            correlationId: $"schedule-{version.WorkflowId}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            cancellationToken);
    }

    private static string ResolveDomainName(string? domainName, string? domainId)
    {
        var name = domainName?.Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        var id = domainId?.Trim();
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        throw new ArgumentException("domainName or domainId is required.");
    }
}
