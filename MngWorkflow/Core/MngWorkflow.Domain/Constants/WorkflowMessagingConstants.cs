namespace MngWorkflow.Domain.Constants;

public static class WorkflowMessagingConstants
{
    public const string Exchange = "mng.workflow";
    public const string ExecutionQueue = "workflow.execution";
    public const string DeadLetterQueue = "workflow.deadletter";
    public const string ExecutionRoutingSuffix = "workflow.exec";

    /// <summary>Retry bucket kuyrukları DLX ile execution kuyruğuna döner.</summary>
    public const string RetryRoutingKey = "system.workflow.exec";

    /// <summary>Delay bucket kuyrukları DLX ile resume kuyruğuna döner.</summary>
    public const string ResumeRoutingKey = "system.workflow.resume";
    public const string ResumeQueue = "workflow.resume";

    public static readonly IReadOnlyList<RetryBucketDefinition> RetryBuckets =
    [
        new("workflow.retry.5s", 5_000),
        new("workflow.retry.30s", 30_000),
        new("workflow.retry.2m", 120_000),
        new("workflow.retry.10m", 600_000)
    ];

    public static readonly IReadOnlyList<RetryBucketDefinition> DelayBuckets =
    [
        new("workflow.delay.5s", 5_000),
        new("workflow.delay.30s", 30_000),
        new("workflow.delay.2m", 120_000),
        new("workflow.delay.10m", 600_000)
    ];
}

public sealed record RetryBucketDefinition(string QueueName, int TtlMilliseconds);
