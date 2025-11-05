namespace MngKeeper.Application.Pipelines;

/// <summary>
/// Base interface for pipeline steps
/// </summary>
/// <typeparam name="TContext">The context type for the pipeline</typeparam>
public interface IPipelineStep<TContext> where TContext : class
{
    /// <summary>
    /// Step name for logging and tracking
    /// </summary>
    string StepName { get; }
    
    /// <summary>
    /// Execute the step
    /// </summary>
    Task<StepResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback the step if needed (compensating transaction)
    /// </summary>
    Task RollbackAsync(TContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a pipeline step execution
/// </summary>
public class StepResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    public static StepResult Success(Dictionary<string, object>? metadata = null)
    {
        return new StepResult 
        { 
            IsSuccess = true,
            Metadata = metadata ?? new()
        };
    }
    
    public static StepResult Failure(string errorMessage, Exception? exception = null)
    {
        return new StepResult 
        { 
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }
}

