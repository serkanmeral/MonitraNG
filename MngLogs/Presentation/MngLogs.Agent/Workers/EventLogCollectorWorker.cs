using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Workers;

public sealed class EventLogCollectorWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly IAgentConfigStore _config;
    private readonly IWindowsEventLogReader _reader;
    private readonly EventLogBookmarkStore _bookmarks;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<EventLogCollectorWorker> _logger;

    public EventLogCollectorWorker(
        IOutboundQueue queue,
        IAgentConfigStore config,
        IWindowsEventLogReader reader,
        EventLogBookmarkStore bookmarks,
        AgentRuntimeStatus status,
        ILogger<EventLogCollectorWorker> logger)
    {
        _queue = queue;
        _config = config;
        _reader = reader;
        _bookmarks = bookmarks;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Event Log collector skipped (non-Windows OS)");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var policy = _config.Current.Policy.EventLog;
            var seconds = Math.Max(5, policy.PollIntervalSeconds);
            try
            {
                if (policy.Enabled)
                    await PollAsync(policy, stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (UnauthorizedAccessException ex)
            {
                _status.MarkEventLogError("Access denied reading Event Log (run as LocalSystem/admin for Security channel)");
                _logger.LogWarning(ex, "Event Log access denied");
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, seconds)), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
            catch (Exception ex)
            {
                _status.MarkEventLogError(ex.Message);
                _logger.LogWarning(ex, "Event Log poll failed");
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task PollAsync(EventLogPolicy policy, CancellationToken cancellationToken)
    {
        var packages = DefaultEventLogPackages.Resolve(policy);
        var max = Math.Max(1, policy.MaxEventsPerPoll);
        var total = 0;
        string? lastAccessError = null;

        foreach (var package in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bookmark = _bookmarks.Get(package.Name);
                var items = _reader.ReadNew(package, bookmark, max, out var updated);
                if (updated != null)
                    _bookmarks.Set(package.Name, updated);

                foreach (var item in items)
                {
                    await _queue.EnqueueAsync(item, cancellationToken);
                    total++;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                lastAccessError = $"Access denied: {package.Channel}/{package.Name}";
                _logger.LogWarning(ex, "Event Log access denied for package {Package}", package.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastAccessError = $"{package.Name}: {ex.Message}";
                _logger.LogWarning(ex, "Event Log package failed {Package}", package.Name);
            }
        }

        if (total > 0)
            _status.MarkEventLogCollected(total);
        else if (lastAccessError != null)
            _status.MarkEventLogError(lastAccessError);
    }
}
