using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Pipelines;

/// <summary>
/// Generic pipeline orchestrator for executing steps in sequence with rollback support
/// </summary>
public class Pipeline<TContext> where TContext : class
{
    private readonly List<IPipelineStep<TContext>> _steps = new();
    private readonly ILogger<Pipeline<TContext>> _logger;
    
    public Pipeline(ILogger<Pipeline<TContext>> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Add a step to the pipeline
    /// </summary>
    public Pipeline<TContext> AddStep(IPipelineStep<TContext> step)
    {
        _steps.Add(step);
        return this;
    }
    
    /// <summary>
    /// Execute all steps in sequence
    /// </summary>
    public async Task<PipelineResult<TContext>> ExecuteAsync(
        TContext context, 
        CancellationToken cancellationToken = default)
    {
        var executedSteps = new Stack<IPipelineStep<TContext>>();
        var result = new PipelineResult<TContext> { Context = context };
        
        _logger.LogInformation("Pipeline execution started with {StepCount} steps", _steps.Count);
        
        try
        {
            foreach (var step in _steps)
            {
                _logger.LogInformation("Executing step: {StepName}", step.StepName);
                
                var stepResult = await step.ExecuteAsync(context, cancellationToken);
                
                result.StepResults.Add(step.StepName, stepResult);
                
                if (!stepResult.IsSuccess)
                {
                    _logger.LogError("Step {StepName} failed: {ErrorMessage}", 
                        step.StepName, stepResult.ErrorMessage);
                    
                    result.IsSuccess = false;
                    result.FailedStepName = step.StepName;
                    result.ErrorMessage = stepResult.ErrorMessage;
                    result.Exception = stepResult.Exception;
                    
                    // Rollback executed steps
                    await RollbackAsync(executedSteps, context, cancellationToken);
                    
                    return result;
                }
                
                _logger.LogInformation("Step {StepName} completed successfully", step.StepName);
                executedSteps.Push(step);
            }
            
            result.IsSuccess = true;
            _logger.LogInformation("Pipeline execution completed successfully");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline execution failed with exception");
            
            result.IsSuccess = false;
            result.ErrorMessage = "Pipeline execution failed";
            result.Exception = ex;
            
            // Rollback on exception
            await RollbackAsync(executedSteps, context, cancellationToken);
            
            return result;
        }
    }
    
    /// <summary>
    /// Rollback executed steps in reverse order
    /// </summary>
    private async Task RollbackAsync(
        Stack<IPipelineStep<TContext>> executedSteps,
        TContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Starting rollback of {Count} executed steps", executedSteps.Count);
        
        while (executedSteps.Count > 0)
        {
            var step = executedSteps.Pop();
            
            try
            {
                _logger.LogInformation("Rolling back step: {StepName}", step.StepName);
                await step.RollbackAsync(context, cancellationToken);
                _logger.LogInformation("Step {StepName} rolled back successfully", step.StepName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback step: {StepName}", step.StepName);
                // Continue rollback even if one fails
            }
        }
        
        _logger.LogWarning("Rollback completed");
    }
}

/// <summary>
/// Result of pipeline execution
/// </summary>
public class PipelineResult<TContext> where TContext : class
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public string? FailedStepName { get; set; }
    public TContext Context { get; set; } = null!;
    public Dictionary<string, StepResult> StepResults { get; set; } = new();
}

