using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowTriggerSyncService(IWorkflowTriggerRepository triggers) : IWorkflowTriggerSyncService
{
    public Task SyncPublishedVersionAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default)
    {
        if (version.Status != WorkflowVersionStatus.Published)
            return Task.CompletedTask;

        var now = DateTime.UtcNow;
        var projections = new List<WorkflowTriggerProjectionDocument>();

        foreach (var trigger in version.Triggers.Where(t => t.Enabled && t.Type == WorkflowTriggerTypes.Event))
        {
            var eventType = ResolveEventType(trigger);
            if (string.IsNullOrWhiteSpace(eventType))
                continue;

            projections.Add(new WorkflowTriggerProjectionDocument
            {
                DomainId = version.DomainId,
                DomainName = version.DomainName,
                WorkflowId = version.WorkflowId,
                WorkflowVersionId = version.Id,
                EventType = eventType,
                FilterExpression = trigger.FilterExpression,
                Enabled = trigger.Enabled,
                UpdatedAt = now
            });
        }

        return triggers.ReplaceForWorkflowAsync(version.DomainName, version.WorkflowId, projections, cancellationToken);
    }

    public static string? ResolveEventType(WorkflowTriggerDefinition trigger)
    {
        if (!trigger.Config.TryGetValue("eventType", out var raw) || raw == null)
            return null;

        return raw.ToString()?.Trim();
    }
}
