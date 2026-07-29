using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Tests;

public class ServiceWatchTransitionsTests
{
    [Theory]
    [InlineData(null, ServiceWatchHealth.Running, ServiceWatchTransition.None)]
    [InlineData(null, ServiceWatchHealth.NotRunning, ServiceWatchTransition.Failed)]
    [InlineData(null, ServiceWatchHealth.Missing, ServiceWatchTransition.Missing)]
    [InlineData(ServiceWatchHealth.Running, ServiceWatchHealth.NotRunning, ServiceWatchTransition.Failed)]
    [InlineData(ServiceWatchHealth.NotRunning, ServiceWatchHealth.Running, ServiceWatchTransition.Recovered)]
    [InlineData(ServiceWatchHealth.Missing, ServiceWatchHealth.Running, ServiceWatchTransition.Recovered)]
    [InlineData(ServiceWatchHealth.Running, ServiceWatchHealth.Running, ServiceWatchTransition.None)]
    [InlineData(ServiceWatchHealth.Running, ServiceWatchHealth.Missing, ServiceWatchTransition.Missing)]
    public void Evaluate_transitions(
        ServiceWatchHealth? previous,
        ServiceWatchHealth current,
        ServiceWatchTransition expected)
    {
        Assert.Equal(expected, ServiceWatchTransitions.Evaluate(previous, current));
    }
}
