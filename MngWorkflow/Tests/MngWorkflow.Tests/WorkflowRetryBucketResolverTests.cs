using MngWorkflow.Domain.Constants;
using MngWorkflow.Infrastructure.Messaging;
using Xunit;

namespace MngWorkflow.Tests.Messaging;

public sealed class WorkflowRetryBucketResolverTests
{
    [Theory]
    [InlineData(1, "workflow.retry.5s")]
    [InlineData(2, "workflow.retry.30s")]
    [InlineData(3, "workflow.retry.2m")]
    [InlineData(4, "workflow.retry.10m")]
    [InlineData(99, "workflow.retry.10m")]
    public void Maps_failed_attempt_to_bucket(int failedAttempt, string expectedQueue)
    {
        var queue = WorkflowRetryBucketResolver.ResolveQueueName(failedAttempt);
        Assert.Equal(expectedQueue, queue);
        Assert.Contains(WorkflowMessagingConstants.RetryBuckets, b => b.QueueName == queue);
    }
}
