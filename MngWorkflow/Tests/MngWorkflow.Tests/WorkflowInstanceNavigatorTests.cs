using MngWorkflow.Infrastructure.Engine;
using Xunit;

namespace MngWorkflow.Tests;

public sealed class WorkflowInstanceNavigatorTests
{
    [Fact]
    public void TryAdvance_single_branch_completes_when_terminal()
    {
        var done = WorkflowInstanceNavigator.TryAdvanceActiveNodes(
            ["log_1"],
            "log_1",
            [],
            out var active);

        Assert.True(done);
        Assert.Empty(active);
    }

    [Fact]
    public void TryAdvance_parallel_branches_complete_when_last_finishes()
    {
        var afterFork = new List<string>();
        Assert.False(WorkflowInstanceNavigator.TryAdvanceActiveNodes(
            ["fork_1"],
            "fork_1",
            ["log_a", "log_b"],
            out afterFork));
        Assert.Equal(["log_a", "log_b"], afterFork);

        var afterA = new List<string>();
        Assert.False(WorkflowInstanceNavigator.TryAdvanceActiveNodes(
            afterFork,
            "log_a",
            [],
            out afterA));
        Assert.Equal(["log_b"], afterA);

        var afterB = new List<string>();
        Assert.True(WorkflowInstanceNavigator.TryAdvanceActiveNodes(
            afterA,
            "log_b",
            [],
            out afterB));
        Assert.Empty(afterB);
    }
}
