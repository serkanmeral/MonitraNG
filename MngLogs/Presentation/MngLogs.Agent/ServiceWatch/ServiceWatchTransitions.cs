namespace MngLogs.Agent.ServiceWatch;

public enum ServiceWatchHealth
{
    Running,
    NotRunning,
    Missing,
    Unknown
}

public enum ServiceWatchTransition
{
    None,
    Failed,
    Recovered,
    Missing
}

/// <summary>Pure transition logic (unit-testable without SCM).</summary>
public static class ServiceWatchTransitions
{
    public static ServiceWatchTransition Evaluate(ServiceWatchHealth? previous, ServiceWatchHealth current)
    {
        if (previous is null)
            return current is ServiceWatchHealth.NotRunning or ServiceWatchHealth.Missing
                ? (current == ServiceWatchHealth.Missing ? ServiceWatchTransition.Missing : ServiceWatchTransition.Failed)
                : ServiceWatchTransition.None;

        if (previous == current)
            return ServiceWatchTransition.None;

        if (current == ServiceWatchHealth.Missing)
            return ServiceWatchTransition.Missing;

        if (current == ServiceWatchHealth.NotRunning && previous == ServiceWatchHealth.Running)
            return ServiceWatchTransition.Failed;

        if (current == ServiceWatchHealth.Running &&
            previous is ServiceWatchHealth.NotRunning or ServiceWatchHealth.Missing or ServiceWatchHealth.Unknown)
            return ServiceWatchTransition.Recovered;

        if (current == ServiceWatchHealth.NotRunning && previous == ServiceWatchHealth.Missing)
            return ServiceWatchTransition.Failed;

        return ServiceWatchTransition.None;
    }

    public static bool IsUnhealthy(ServiceWatchHealth health) =>
        health is ServiceWatchHealth.NotRunning or ServiceWatchHealth.Missing;
}
