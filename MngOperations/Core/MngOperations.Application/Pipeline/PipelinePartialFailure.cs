using MngOperations.Application.Exceptions;

namespace MngOperations.Application.Pipeline;

public sealed class PipelineContext
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
    public List<string> CompletedSteps { get; } = new();
}

public static class PipelineSteps
{
    public const string PersistWorkItem = "persistWorkItem";
    public const string PersistTimelineSegment = "persistTimelineSegment";
    public const string PersistActivity = "persistActivity";
    public const string PersistComment = "persistComment";
    public const string AutomationRules = "automationRules";
    public const string PublishRabbitMq = "publishRabbitMq";
    public const string DispatchNotifications = "dispatchNotifications";
}

public static class PipelinePartialFailure
{
    public const string Code = "PARTIAL_FAILURE";

    public static OperationCoreException Create(
        string failedStep,
        PipelineContext context,
        Exception inner,
        IReadOnlyDictionary<string, object?>? workItemSnapshot = null)
    {
        var details = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["completedSteps"] = context.CompletedSteps.ToList(),
            ["failedStep"] = failedStep,
            ["correlationId"] = context.CorrelationId.ToString(),
            ["innerError"] = inner.Message
        };

        if (workItemSnapshot != null)
            details["workItem"] = workItemSnapshot;

        return new OperationCoreException(
            Code,
            $"Pipeline failed at step '{failedStep}'. Completed: {string.Join(", ", context.CompletedSteps)}",
            $"Pipeline '{failedStep}' adımında başarısız oldu; önceki adımlar uygulandı.",
            500,
            details);
    }
}
