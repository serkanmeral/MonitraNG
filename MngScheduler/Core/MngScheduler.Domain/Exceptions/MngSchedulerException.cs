namespace MngScheduler.Domain.Exceptions;

/// <summary>
/// Base exception for MngScheduler domain
/// </summary>
public class MngSchedulerException : Exception
{
    public MngSchedulerException(string message) : base(message)
    {
    }

    public MngSchedulerException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when cron expression is invalid
/// </summary>
public class InvalidCronExpressionException : MngSchedulerException
{
    public InvalidCronExpressionException(string cronExpression) 
        : base($"Invalid cron expression: {cronExpression}")
    {
    }
}

/// <summary>
/// Exception thrown when job is not found
/// </summary>
public class JobNotFoundException : MngSchedulerException
{
    public JobNotFoundException(string jobId) 
        : base($"Job not found: {jobId}")
    {
    }
}

/// <summary>
/// Exception thrown when job execution fails
/// </summary>
public class JobExecutionException : MngSchedulerException
{
    public JobExecutionException(string message) : base(message)
    {
    }

    public JobExecutionException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
