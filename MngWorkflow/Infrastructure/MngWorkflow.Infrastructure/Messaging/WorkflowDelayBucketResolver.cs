using MngWorkflow.Domain.Constants;

namespace MngWorkflow.Infrastructure.Messaging;

public static class WorkflowDelayBucketResolver
{
    /// <summary>
    /// İstenen gecikme süresine eşit veya daha uzun TTL'li en küçük delay bucket'ı seçer.
    /// </summary>
    public static string ResolveQueueName(int delaySeconds)
    {
        if (delaySeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(delaySeconds), "delaySeconds must be positive.");

        var buckets = WorkflowMessagingConstants.DelayBuckets;
        if (buckets.Count == 0)
            throw new InvalidOperationException("No delay buckets configured.");

        var delayMs = delaySeconds * 1000L;
        foreach (var bucket in buckets.OrderBy(b => b.TtlMilliseconds))
        {
            if (bucket.TtlMilliseconds >= delayMs)
                return bucket.QueueName;
        }

        return buckets[^1].QueueName;
    }
}
