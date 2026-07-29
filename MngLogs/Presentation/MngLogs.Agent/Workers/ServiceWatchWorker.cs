using System.Runtime.Versioning;
using System.ServiceProcess;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Agent.Workers;

public sealed class ServiceWatchWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly IAgentConfigStore _config;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<ServiceWatchWorker> _logger;
    private readonly Dictionary<string, ServiceWatchHealth> _previous = new(StringComparer.OrdinalIgnoreCase);

    public ServiceWatchWorker(
        IOutboundQueue queue,
        IAgentConfigStore config,
        AgentRuntimeStatus status,
        ILogger<ServiceWatchWorker> logger)
    {
        _queue = queue;
        _config = config;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Service watch skipped (non-Windows OS)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var policy = _config.Current.Policy.ServiceWatch;
            var seconds = Math.Max(5, policy.PollIntervalSeconds);
            try
            {
                if (policy.Enabled && policy.Services.Count > 0)
                    await PollAsync(policy, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _status.MarkServiceWatchError(ex.Message);
                _logger.LogWarning(ex, "Service watch poll failed");
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task PollAsync(ServiceWatchPolicy policy, CancellationToken cancellationToken)
    {
        var emitted = 0;
        foreach (var svc in policy.Services.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = svc.Name.Trim();
            var health = Probe(name, out var statusText, out var displayName);
            _status.UpdateServiceWatchSnapshot(
                name,
                health.ToString(),
                statusText,
                displayName,
                svc.RestartAllowed);

            var hadPrev = _previous.ContainsKey(name);
            var transition = ServiceWatchTransitions.Evaluate(
                hadPrev ? _previous[name] : null,
                health);
            _previous[name] = health;

            if (transition == ServiceWatchTransition.None)
                continue;

            await _queue.EnqueueAsync(
                BuildEvent(name, displayName, statusText, transition, svc.RestartAllowed),
                cancellationToken);
            emitted++;

            if (svc.RestartAllowed &&
                transition is ServiceWatchTransition.Failed or ServiceWatchTransition.Missing)
            {
                var restarted = TryRestart(name, out var restartError);
                await _queue.EnqueueAsync(
                    BuildRestartEvent(name, displayName, restarted, restartError),
                    cancellationToken);
                emitted++;
            }
        }

        if (emitted > 0)
            _status.MarkServiceWatchEvents(emitted);
    }

    [SupportedOSPlatform("windows")]
    private static ServiceWatchHealth Probe(string name, out string statusText, out string? displayName)
    {
        displayName = null;
        try
        {
            using var sc = new ServiceController(name);
            displayName = sc.DisplayName;
            sc.Refresh();
            statusText = sc.Status.ToString();
            return sc.Status == ServiceControllerStatus.Running
                ? ServiceWatchHealth.Running
                : ServiceWatchHealth.NotRunning;
        }
        catch (InvalidOperationException)
        {
            statusText = "Missing";
            return ServiceWatchHealth.Missing;
        }
        catch (Exception ex)
        {
            statusText = ex.GetType().Name;
            return ServiceWatchHealth.Unknown;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryRestart(string name, out string? error)
    {
        error = null;
        try
        {
            using var sc = new ServiceController(name);
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
                return true;

            if (sc.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
            {
                if (sc.Status == ServiceControllerStatus.StopPending)
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                return true;
            }

            error = $"Cannot start from status {sc.Status}";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static IngestEventItem BuildEvent(
        string name,
        string? displayName,
        string statusText,
        ServiceWatchTransition transition,
        bool restartAllowed)
    {
        var (action, severity) = transition switch
        {
            ServiceWatchTransition.Failed => ("service.failed", "error"),
            ServiceWatchTransition.Recovered => ("service.recovered", "info"),
            ServiceWatchTransition.Missing => ("service.missing", "error"),
            _ => ("service.unknown", "warning")
        };

        return new IngestEventItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "service-watch",
            SourceProduct = "mnglogs-agent",
            Severity = severity,
            Message = action,
            Fields = new Dictionary<string, object?>
            {
                ["serviceName"] = name,
                ["displayName"] = displayName,
                ["status"] = statusText,
                ["transition"] = transition.ToString(),
                ["restartAllowed"] = restartAllowed,
                ["machine"] = Environment.MachineName
            }
        };
    }

    private static IngestEventItem BuildRestartEvent(string name, string? displayName, bool ok, string? error) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "service-watch",
            SourceProduct = "mnglogs-agent",
            Severity = ok ? "info" : "warning",
            Message = ok ? "service.restart.ok" : "service.restart.failed",
            Fields = new Dictionary<string, object?>
            {
                ["serviceName"] = name,
                ["displayName"] = displayName,
                ["restartOk"] = ok,
                ["error"] = error,
                ["machine"] = Environment.MachineName
            }
        };
}
