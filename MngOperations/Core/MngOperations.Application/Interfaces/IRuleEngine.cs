using MngOperations.Application.Rules;

namespace MngOperations.Application.Interfaces;

public interface IRuleEngine
{
    Task<RuleExecutionResult> ExecuteAsync(
        RuleExecutionContext context,
        RulePhase phase,
        CancellationToken cancellationToken = default);
}
