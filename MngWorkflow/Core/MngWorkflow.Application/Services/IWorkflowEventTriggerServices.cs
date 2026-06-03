using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Application.Services;

public interface IWorkflowEventTriggerProcessor
{
    Task ProcessAsync(string exchange, string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default);
}

public interface IWorkflowTriggerSyncService
{
    Task SyncPublishedVersionAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default);
}
