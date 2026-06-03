using MngWorkflow.Infrastructure.Messaging;
using MngWorkflow.Infrastructure.Utilities;
using Xunit;

namespace MngWorkflow.Tests;

public sealed class WorkflowDelayBucketResolverTests
{
    [Theory]
    [InlineData(1, "workflow.delay.5s")]
    [InlineData(5, "workflow.delay.5s")]
    [InlineData(6, "workflow.delay.30s")]
    [InlineData(30, "workflow.delay.30s")]
    [InlineData(45, "workflow.delay.2m")]
    [InlineData(60, "workflow.delay.2m")]
    [InlineData(600, "workflow.delay.10m")]
    [InlineData(3600, "workflow.delay.10m")]
    public void ResolveQueueName_PicksSmallestBucketCoveringDelay(int delaySeconds, string expectedQueue)
    {
        var queue = WorkflowDelayBucketResolver.ResolveQueueName(delaySeconds);
        Assert.Equal(expectedQueue, queue);
    }

    [Fact]
    public void ToOneShotCron_UsesUtcParts()
    {
        var cron = WorkflowDelayCronHelper.ToOneShotCron(new DateTime(2026, 6, 3, 5, 15, 7, DateTimeKind.Utc));
        Assert.Equal("7 15 5 3 6 ? 2026", cron);
    }
}
