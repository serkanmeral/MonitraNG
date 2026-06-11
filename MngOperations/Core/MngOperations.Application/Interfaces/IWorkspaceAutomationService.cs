using MngOperations.Application.Contracts.Automations;

namespace MngOperations.Application.Interfaces;

public interface IWorkspaceAutomationService
{
    Task ExecuteOnWorkItemTransitionAsync(
        WorkspaceAutomationTriggerContext context,
        string token,
        CancellationToken cancellationToken = default);

    Task<SimulateWorkspaceAutomationResult> SimulateAsync(
        string automationId,
        SimulateWorkspaceAutomationRequest request,
        CancellationToken cancellationToken = default);
}
