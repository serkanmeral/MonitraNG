using MngWorkflow.Domain.Constants;

namespace MngWorkflow.Infrastructure.Messaging;

public static class WorkflowRetryBucketResolver
{
    /// <summary>
    /// Başarısız attempt numarasına göre en yakın üst retry bucket kuyruğunu seçer.
    /// attempt=1 → 5s, attempt=2 → 30s, attempt=3 → 2m, attempt≥4 → 10m
    /// </summary>
    public static string ResolveQueueName(int failedAttempt)
    {
        var buckets = WorkflowMessagingConstants.RetryBuckets;
        if (buckets.Count == 0)
            throw new InvalidOperationException("No retry buckets configured.");

        var index = Math.Clamp(failedAttempt - 1, 0, buckets.Count - 1);
        return buckets[index].QueueName;
    }
}
