using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Linux.Journal;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Linux.Workers;

/// <summary>Polls journald packages (sshd/sudo/unit-fail) via journalctl (P3c).</summary>
public sealed class LinuxJournalCollectorWorker : BackgroundService
{
    private readonly IOutboundQueue _queue;
    private readonly IAgentConfigStore _config;
    private readonly JournalBookmarkStore _bookmarks;
    private readonly JournalctlReader _reader;
    private readonly AgentRuntimeStatus _status;
    private readonly ILogger<LinuxJournalCollectorWorker> _logger;

    public LinuxJournalCollectorWorker(
        IOutboundQueue queue,
        IAgentConfigStore config,
        JournalBookmarkStore bookmarks,
        JournalctlReader reader,
        AgentRuntimeStatus status,
        ILogger<LinuxJournalCollectorWorker> logger)
    {
        _queue = queue;
        _config = config;
        _bookmarks = bookmarks;
        _reader = reader;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var policy = _config.Current.Policy.Journal ?? new JournalPolicy();
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
            catch (Exception ex)
            {
                _status.MarkJournalError(ex.Message);
                _logger.LogWarning(ex, "Journal poll failed");
                try { await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task PollAsync(JournalPolicy policy, CancellationToken cancellationToken)
    {
        var packages = BuiltinJournalPackages.Resolve(policy);
        var max = Math.Max(1, policy.MaxEventsPerPoll);
        var total = 0;

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Journal package {Package} failed", package.Name);
                _status.MarkJournalError($"{package.Name}: {ex.Message}");
            }
        }

        if (total > 0)
            _status.MarkJournalCollected(total);
        else
            _status.MarkJournalIdle();
    }
}
