using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Linux.ServiceWatch;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;
using MngLogs.Agent.ServiceWatch;

namespace MngLogs.Agent.Linux.Workers;

/// <summary>Watches systemd units and selected application processes (P3b).</summary>
public sealed class LinuxServiceWatchWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly IAgentConfigStore _config;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<LinuxServiceWatchWorker> _logger;
    private readonly Dictionary<string, ServiceWatchHealth> _previous = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastRestartAttemptUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _restartAttempts = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastInventoryShipUtc = DateTime.MinValue;

    public LinuxServiceWatchWorker(
        IOutboundQueue queue,
        IAgentConfigStore config,
        AgentRuntimeStatus status,
        ILogger<LinuxServiceWatchWorker> logger)
    {
        _queue = queue;
        _config = config;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var policy = _config.Current.Policy.ServiceWatch;
            var seconds = Math.Max(5, policy.PollIntervalSeconds);
            try
            {
                var hasTargets = policy.Services.Count > 0 || policy.Applications.Count > 0;
                if (policy.Enabled && hasTargets)
                    await PollAsync(policy, stoppingToken);
                else
                    await ClearWatchStateAsync(policy, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _status.MarkServiceWatchError(ex.Message);
                _logger.LogWarning(ex, "Linux watch poll failed");
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task PollAsync(ServiceWatchPolicy policy, CancellationToken cancellationToken)
    {
        var emitted = 0;
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var svc in policy.Services.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = SystemdUnitProbe.NormalizeUnitName(svc.Name);
            var key = $"service:{name}";
            activeKeys.Add(key);

            var (health, statusText, description) = SystemdUnitProbe.Probe(name);
            _status.UpdateServiceWatchSnapshot(
                "service", name, health.ToString(), statusText, description, svc.RestartAllowed);

            emitted += await EmitServiceTransitionAsync(
                key, name, description, statusText, health, svc.RestartAllowed, cancellationToken);

            if (svc.RestartAllowed && ServiceWatchTransitions.IsUnhealthy(health))
                emitted += await MaybeRestartUnitAsync(policy, key, name, description, cancellationToken);
            else if (health == ServiceWatchHealth.Running &&
                     _restartAttempts.TryGetValue(key, out var n) && n > 0)
            {
                _restartAttempts[key] = 0;
                _status.ResetServiceRestartAttempts(name);
            }
        }

        foreach (var app in policy.Applications.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = ApplicationWatchProbe.NormalizeProcessName(app.Name);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var min = app.MinCount <= 0 ? 1 : app.MinCount;
            var key = $"application:{name}";
            activeKeys.Add(key);
            var count = ApplicationWatchProbe.CountInstances(name);
            var health = count >= min ? ServiceWatchHealth.Running : ServiceWatchHealth.Missing;
            var statusText = $"instances={count}/{min}";
            _status.UpdateServiceWatchSnapshot(
                "application", name, health.ToString(), statusText, null, app.RestartAllowed, count, min);

            emitted += await EmitAppTransitionAsync(key, name, count, min, health, cancellationToken);

            if (app.RestartAllowed && ServiceWatchTransitions.IsUnhealthy(health))
                emitted += await MaybeRestartAppAsync(policy, key, name, app, cancellationToken);
            else if (health == ServiceWatchHealth.Running &&
                     _restartAttempts.TryGetValue(key, out var n) && n > 0)
            {
                _restartAttempts[key] = 0;
                _status.ResetWatchRestartAttempts("application", name);
            }
        }

        var pruned = _status.PruneServiceWatchSnapshot(activeKeys);
        PruneLocalWatchState(activeKeys);
        if (pruned > 0)
            _lastInventoryShipUtc = DateTime.MinValue;

        emitted += await MaybeShipInventoryAsync(policy, cancellationToken);

        if (emitted > 0)
            _status.MarkServiceWatchEvents(emitted);
        else
            _status.MarkServiceWatchIdle();
    }

    private async Task ClearWatchStateAsync(ServiceWatchPolicy policy, CancellationToken cancellationToken)
    {
        var pruned = _status.PruneServiceWatchSnapshot([]);
        PruneLocalWatchState([]);
        if (pruned <= 0)
            return;

        _lastInventoryShipUtc = DateTime.MinValue;
        if (policy.IncludeInventory)
        {
            _lastInventoryShipUtc = DateTime.UtcNow;
            await _queue.EnqueueAsync(
                WatchInventoryEvents.Build(_status.ServiceWatchSnapshot(), DateTime.UtcNow),
                cancellationToken);
            _status.MarkServiceWatchEvents(1);
        }
        else
        {
            _status.MarkServiceWatchIdle();
        }
    }

    private void PruneLocalWatchState(HashSet<string> activeKeys)
    {
        foreach (var key in _previous.Keys.ToArray())
            if (!activeKeys.Contains(key)) _previous.Remove(key);
        foreach (var key in _lastRestartAttemptUtc.Keys.ToArray())
            if (!activeKeys.Contains(key)) _lastRestartAttemptUtc.Remove(key);
        foreach (var key in _restartAttempts.Keys.ToArray())
            if (!activeKeys.Contains(key)) _restartAttempts.Remove(key);
    }

    private async Task<int> MaybeShipInventoryAsync(ServiceWatchPolicy policy, CancellationToken cancellationToken)
    {
        if (!policy.IncludeInventory)
            return 0;

        var interval = Math.Max(15, policy.InventoryIntervalSeconds);
        if (DateTime.UtcNow - _lastInventoryShipUtc < TimeSpan.FromSeconds(interval))
            return 0;

        var snapshot = _status.ServiceWatchSnapshot();
        if (snapshot.Count == 0 && policy.Services.Count == 0 && policy.Applications.Count == 0)
            return 0;

        _lastInventoryShipUtc = DateTime.UtcNow;
        await _queue.EnqueueAsync(WatchInventoryEvents.Build(snapshot, DateTime.UtcNow), cancellationToken);
        return 1;
    }

    private async Task<int> EmitServiceTransitionAsync(
        string key,
        string name,
        string? displayName,
        string statusText,
        ServiceWatchHealth health,
        bool restartAllowed,
        CancellationToken cancellationToken)
    {
        var hadPrev = _previous.ContainsKey(key);
        var transition = ServiceWatchTransitions.Evaluate(hadPrev ? _previous[key] : null, health);
        _previous[key] = health;
        if (transition == ServiceWatchTransition.None)
            return 0;

        var (action, severity) = transition switch
        {
            ServiceWatchTransition.Failed => ("service.failed", "error"),
            ServiceWatchTransition.Recovered => ("service.recovered", "info"),
            ServiceWatchTransition.Missing => ("service.missing", "error"),
            _ => ("service.unknown", "warning")
        };

        if (transition == ServiceWatchTransition.Recovered)
        {
            _restartAttempts[key] = 0;
            _status.ResetServiceRestartAttempts(name);
        }

        await _queue.EnqueueAsync(new IngestEventItem
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
                ["platform"] = "linux",
                ["machine"] = Environment.MachineName
            }
        }, cancellationToken);

        return 1;
    }

    private async Task<int> MaybeRestartUnitAsync(
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
            return 0;

        _lastRestartAttemptUtc[key] = DateTime.UtcNow;
        attempts++;
        _restartAttempts[key] = attempts;

        var restarted = SystemdUnitProbe.TryRestart(name, out var restartError);
        _status.NoteServiceRestart(name, restarted, restartError, attempts);
        await _queue.EnqueueAsync(new IngestEventItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "service-watch",
            SourceProduct = "mnglogs-agent",
            Severity = restarted ? "info" : "warning",
            Message = restarted ? "service.restart.ok" : "service.restart.failed",
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = restarted ? "service.restart.ok" : "service.restart.failed",
                ["watchKind"] = "service",
                ["serviceName"] = name,
                ["displayName"] = displayName,
                ["restartOk"] = restarted,
                ["restartError"] = restartError,
                ["attempt"] = attempts,
                ["maxAttempts"] = maxAttempts,
                ["platform"] = "linux",
                ["machine"] = Environment.MachineName
            }
        }, cancellationToken);

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
        var transition = ServiceWatchTransitions.Evaluate(hadPrev ? _previous[key] : null, health);
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
                ["platform"] = "linux",
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
            return 0;

        _lastRestartAttemptUtc[key] = DateTime.UtcNow;
        attempts++;
        _restartAttempts[key] = attempts;

        var restarted = ApplicationWatchProbe.TryStart(
            app.ExecutablePath, app.Arguments, app.WorkingDirectory, out var restartError, out var pid);
        _status.NoteWatchRestart("application", name, restarted, restartError, attempts);
        await _queue.EnqueueAsync(new IngestEventItem
        {
            Id = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Source = "app-watch",
            SourceProduct = "mnglogs-agent",
            Severity = restarted ? "info" : "warning",
            Message = restarted ? "app.restart.ok" : "app.restart.failed",
            Fields = new Dictionary<string, object?>
            {
                ["event.action"] = restarted ? "app.restart.ok" : "app.restart.failed",
                ["watchKind"] = "application",
                ["processName"] = name,
                ["executablePath"] = app.ExecutablePath,
                ["restartOk"] = restarted,
                ["restartError"] = restartError,
                ["pid"] = pid,
                ["attempt"] = attempts,
                ["maxAttempts"] = maxAttempts,
                ["platform"] = "linux",
                ["machine"] = Environment.MachineName
            }
        }, cancellationToken);

        return 1;
    }
}
