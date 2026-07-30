using System.Runtime.Versioning;
using System.ServiceProcess;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Agent.Workers;

/// <summary>Watches Windows services and selected application processes.</summary>
public sealed class ServiceWatchWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly IAgentConfigStore _config;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<ServiceWatchWorker> _logger;
    private readonly Dictionary<string, ServiceWatchHealth> _previous = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastRestartAttemptUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _restartAttempts = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastInventoryShipUtc = DateTime.MinValue;

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
            _logger.LogInformation("Service/application watch skipped (non-Windows OS)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var policy = _config.Current.Policy.ServiceWatch;
            var seconds = Math.Max(5, policy.PollIntervalSeconds);
            try
            {
                var hasTargets = policy.Services.Count > 0 || policy.Applications.Count > 0;
                if (policy.Enabled && hasTargets)
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
                _logger.LogWarning(ex, "Watch poll failed");
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
            var key = $"service:{name}";
            var health = ProbeService(name, out var statusText, out var displayName);
            _status.UpdateServiceWatchSnapshot(
                "service", name, health.ToString(), statusText, displayName, svc.RestartAllowed);

            emitted += await EmitServiceTransitionAsync(
                policy, key, name, displayName, statusText, health, svc.RestartAllowed, cancellationToken);

            if (svc.RestartAllowed && ServiceWatchTransitions.IsUnhealthy(health))
            {
                emitted += await MaybeRestartUnhealthyAsync(
                    policy, key, name, displayName, cancellationToken);
            }
            else if (health == ServiceWatchHealth.Running)
            {
                if (_restartAttempts.TryGetValue(key, out var n) && n > 0)
                {
                    _restartAttempts[key] = 0;
                    _status.ResetServiceRestartAttempts(name);
                }
            }
        }

        foreach (var app in policy.Applications.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = ApplicationWatchProbe.NormalizeProcessName(app.Name);
            var min = app.MinCount <= 0 ? 1 : app.MinCount;
            var key = $"application:{name}";
            var count = ApplicationWatchProbe.CountInstances(name);
            var health = count >= min ? ServiceWatchHealth.Running : ServiceWatchHealth.Missing;
            var statusText = $"instances={count}/{min}";
            _status.UpdateServiceWatchSnapshot(
                "application",
                name,
                health.ToString(),
                statusText,
                displayName: null,
                restartAllowed: app.RestartAllowed,
                instanceCount: count,
                minCount: min);

            emitted += await EmitAppTransitionAsync(key, name, count, min, health, cancellationToken);

            if (app.RestartAllowed && ServiceWatchTransitions.IsUnhealthy(health))
            {
                emitted += await MaybeRestartAppAsync(policy, key, name, app, cancellationToken);
            }
            else if (health == ServiceWatchHealth.Running)
            {
                if (_restartAttempts.TryGetValue(key, out var n) && n > 0)
                {
                    _restartAttempts[key] = 0;
                    _status.ResetWatchRestartAttempts("application", name);
                }
            }
        }

        emitted += await MaybeShipInventoryAsync(policy, cancellationToken);

        if (emitted > 0)
            _status.MarkServiceWatchEvents(emitted);
        else
            _status.MarkServiceWatchIdle();
    }

    private async Task<int> MaybeShipInventoryAsync(
        ServiceWatchPolicy policy,
        CancellationToken cancellationToken)
    {
        if (!policy.IncludeInventory)
            return 0;

        var interval = Math.Max(15, policy.InventoryIntervalSeconds);
        if (DateTime.UtcNow - _lastInventoryShipUtc < TimeSpan.FromSeconds(interval))
            return 0;

        var snapshot = _status.ServiceWatchSnapshot();
        if (snapshot.Count == 0 &&
            policy.Services.Count == 0 &&
            policy.Applications.Count == 0)
        {
            return 0;
        }

        _lastInventoryShipUtc = DateTime.UtcNow;
        await _queue.EnqueueAsync(WatchInventoryEvents.Build(snapshot, DateTime.UtcNow), cancellationToken);
        return 1;
    }

    private async Task<int> EmitServiceTransitionAsync(
        ServiceWatchPolicy policy,
        string key,
        string name,
        string? displayName,
        string statusText,
        ServiceWatchHealth health,
        bool restartAllowed,
        CancellationToken cancellationToken)
    {
        var emitted = 0;
        var hadPrev = _previous.ContainsKey(key);
        var transition = ServiceWatchTransitions.Evaluate(
            hadPrev ? _previous[key] : null,
            health);
        _previous[key] = health;

        if (transition == ServiceWatchTransition.None)
            return 0;

        await _queue.EnqueueAsync(
            BuildServiceEvent(name, displayName, statusText, transition, restartAllowed),
            cancellationToken);
        emitted++;

        if (transition == ServiceWatchTransition.Recovered)
        {
            _restartAttempts[key] = 0;
            _status.ResetServiceRestartAttempts(name);
            return emitted;
        }

        // Restart retries are handled by MaybeRestartUnhealthyAsync on each poll.
        return emitted;
    }

    [SupportedOSPlatform("windows")]
    private async Task<int> MaybeRestartUnhealthyAsync(
        ServiceWatchPolicy policy,
        string key,
        string name,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var cooldown = Math.Max(30, policy.RestartCooldownSeconds);
        var maxAttempts = Math.Max(1, policy.RestartMaxAttempts);
        _restartAttempts.TryGetValue(key, out var attempts);

        if (attempts >= maxAttempts)
            return 0;

        if (_lastRestartAttemptUtc.TryGetValue(key, out var last) &&
            DateTime.UtcNow - last < TimeSpan.FromSeconds(cooldown))
        {
            return 0;
        }

        // First attempt may coincide with the failure transition; still emit restart event once.
        _lastRestartAttemptUtc[key] = DateTime.UtcNow;
        attempts++;
        _restartAttempts[key] = attempts;

        var restarted = TryRestart(name, out var restartError);
        _status.NoteServiceRestart(name, restarted, restartError, attempts);
        await _queue.EnqueueAsync(
            BuildRestartEvent(name, displayName, restarted, restartError, attempts, maxAttempts),
            cancellationToken);

        if (attempts >= maxAttempts && !restarted)
        {
            await _queue.EnqueueAsync(
                BuildRestartSkippedEvent(name, displayName, "max_attempts", attempts, maxAttempts, cooldown),
                cancellationToken);
            return 2;
        }

        return 1;
    }

    private async Task<int> EmitAppTransitionAsync(
        string key,
        string name,
        int count,
        int minCount,
        ServiceWatchHealth health,
        CancellationToken cancellationToken)
    {
        var hadPrev = _previous.ContainsKey(key);
        var transition = ServiceWatchTransitions.Evaluate(
            hadPrev ? _previous[key] : null,
            health);
        _previous[key] = health;

        if (transition == ServiceWatchTransition.None)
            return 0;

        var (action, severity) = transition switch
        {
            ServiceWatchTransition.Missing => ("app.missing", "error"),
            ServiceWatchTransition.Recovered => ("app.recovered", "info"),
            ServiceWatchTransition.Failed => ("app.missing", "error"),
            _ => ("app.unknown", "warning")
        };

        if (transition == ServiceWatchTransition.Recovered)
        {
            _restartAttempts[key] = 0;
            _status.ResetWatchRestartAttempts("application", name);
        }

        await _queue.EnqueueAsync(new IngestEventItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "app-watch",
            SourceProduct = "mnglogs-agent",
            Severity = severity,
            Message = action,
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = action,
                ["watchKind"] = "application",
                ["processName"] = name,
                ["instanceCount"] = count,
                ["minCount"] = minCount,
                ["transition"] = transition.ToString(),
                ["machine"] = Environment.MachineName
            }
        }, cancellationToken);

        return 1;
    }

    private async Task<int> MaybeRestartAppAsync(
        ServiceWatchPolicy policy,
        string key,
        string name,
        WatchedApplication app,
        CancellationToken cancellationToken)
    {
        var cooldown = Math.Max(30, policy.RestartCooldownSeconds);
        var maxAttempts = Math.Max(1, policy.RestartMaxAttempts);
        _restartAttempts.TryGetValue(key, out var attempts);

        if (attempts >= maxAttempts)
            return 0;

        if (_lastRestartAttemptUtc.TryGetValue(key, out var last) &&
            DateTime.UtcNow - last < TimeSpan.FromSeconds(cooldown))
        {
            return 0;
        }

        _lastRestartAttemptUtc[key] = DateTime.UtcNow;
        attempts++;
        _restartAttempts[key] = attempts;

        var restarted = ApplicationWatchProbe.TryStart(
            app.ExecutablePath,
            app.Arguments,
            app.WorkingDirectory,
            out var restartError,
            out var pid);
        _status.NoteWatchRestart("application", name, restarted, restartError, attempts);
        await _queue.EnqueueAsync(
            BuildAppRestartEvent(name, app.ExecutablePath, restarted, restartError, attempts, maxAttempts, pid),
            cancellationToken);

        if (attempts >= maxAttempts && !restarted)
        {
            await _queue.EnqueueAsync(
                BuildAppRestartSkippedEvent(name, "max_attempts", attempts, maxAttempts, cooldown),
                cancellationToken);
            return 2;
        }

        return 1;
    }

    [SupportedOSPlatform("windows")]
    private static ServiceWatchHealth ProbeService(string name, out string statusText, out string? displayName)
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

    private static IngestEventItem BuildServiceEvent(
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
                ["event.action"] = action,
                ["watchKind"] = "service",
                ["serviceName"] = name,
                ["displayName"] = displayName,
                ["status"] = statusText,
                ["transition"] = transition.ToString(),
                ["restartAllowed"] = restartAllowed,
                ["machine"] = Environment.MachineName
            }
        };
    }

    private static IngestEventItem BuildRestartEvent(
        string name,
        string? displayName,
        bool ok,
        string? error,
        int attempt,
        int maxAttempts) =>
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
                ["event.action"] = ok ? "service.restart.ok" : "service.restart.failed",
                ["watchKind"] = "service",
                ["serviceName"] = name,
                ["displayName"] = displayName,
                ["restartOk"] = ok,
                ["error"] = error,
                ["restartAttempt"] = attempt,
                ["restartMaxAttempts"] = maxAttempts,
                ["machine"] = Environment.MachineName
            }
        };

    private static IngestEventItem BuildRestartSkippedEvent(
        string name,
        string? displayName,
        string reason,
        int attempt,
        int maxAttempts,
        int cooldownSeconds) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "service-watch",
            SourceProduct = "mnglogs-agent",
            Severity = "warning",
            Message = "service.restart.skipped",
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = "service.restart.skipped",
                ["watchKind"] = "service",
                ["serviceName"] = name,
                ["displayName"] = displayName,
                ["reason"] = reason,
                ["restartAttempt"] = attempt,
                ["restartMaxAttempts"] = maxAttempts,
                ["restartCooldownSeconds"] = cooldownSeconds,
                ["machine"] = Environment.MachineName
            }
        };

    private static IngestEventItem BuildAppRestartEvent(
        string name,
        string? executablePath,
        bool ok,
        string? error,
        int attempt,
        int maxAttempts,
        int? pid) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "app-watch",
            SourceProduct = "mnglogs-agent",
            Severity = ok ? "info" : "warning",
            Message = ok ? "app.restart.ok" : "app.restart.failed",
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = ok ? "app.restart.ok" : "app.restart.failed",
                ["watchKind"] = "application",
                ["processName"] = name,
                ["executablePath"] = executablePath,
                ["restartOk"] = ok,
                ["error"] = error,
                ["pid"] = pid,
                ["restartAttempt"] = attempt,
                ["restartMaxAttempts"] = maxAttempts,
                ["machine"] = Environment.MachineName
            }
        };

    private static IngestEventItem BuildAppRestartSkippedEvent(
        string name,
        string reason,
        int attempt,
        int maxAttempts,
        int cooldownSeconds) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "app-watch",
            SourceProduct = "mnglogs-agent",
            Severity = "warning",
            Message = "app.restart.skipped",
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = "app.restart.skipped",
                ["watchKind"] = "application",
                ["processName"] = name,
                ["reason"] = reason,
                ["restartAttempt"] = attempt,
                ["restartMaxAttempts"] = maxAttempts,
                ["restartCooldownSeconds"] = cooldownSeconds,
                ["machine"] = Environment.MachineName
            }
        };
}
