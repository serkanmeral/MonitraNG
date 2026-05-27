using System.Text.Json;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;

namespace MngOperations.Application.Utilities;

public static class StateFlowTransitionResolver
{
    /// <summary>
    /// from-origin: initialTransitionKey ile flow kataloğundan hedef state (from = flow.initialStateId).
    /// </summary>
    public static string ResolveCreateStateId(
        StateFlowRecord stateFlow,
        string? initialTransitionKey,
        string defaultInitialStateId)
    {
        if (string.IsNullOrWhiteSpace(initialTransitionKey))
            return defaultInitialStateId;

        var match = StateFlowCatalog.FindTransition(stateFlow.Transitions, initialTransitionKey.Trim(), defaultInitialStateId);
        var toStateId = match.HasValue ? StateFlowCatalog.GetToStateId(match.Value) : null;
        if (string.IsNullOrEmpty(toStateId))
        {
            throw new OperationCoreException(
                "TRANSITION_NOT_FOUND",
                $"Transition '{initialTransitionKey}' not found from initial state.",
                $"Transition '{initialTransitionKey}' başlangıç state'inden bulunamadı.",
                400);
        }

        return toStateId;
    }

}
