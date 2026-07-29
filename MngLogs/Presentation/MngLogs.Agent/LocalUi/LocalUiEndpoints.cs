using System.Text.Json;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.EventLog;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.LocalUi;

public static class LocalUiEndpoints
{
    public static void MapLocalUi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/status", (IAgentConfigStore config, AgentRuntimeStatus status, IOutboundQueue queue) =>
        {
            var s = config.Current;
            return Results.Json(new
            {
                service = "mnglogs-agent",
                startedAtUtc = status.StartedAtUtc,
                hostId = config.ResolveHostId(),
                hostname = Environment.MachineName,
                domain = s.Policy.Domain,
                collectorBaseUrl = s.System.CollectorBaseUrl,
                collectorHealthy = status.CollectorHealthy,
                queuePending = queue.PendingCount,
                lastHeartbeatUtc = status.LastHeartbeatUtc,
                lastEventLogUtc = status.LastEventLogUtc,
                lastEventLogError = status.LastEventLogError,
                lastServiceWatchUtc = status.LastServiceWatchUtc,
                lastServiceWatchError = status.LastServiceWatchError,
                lastShipUtc = status.LastShipUtc,
                lastShipSuccessUtc = status.LastShipSuccessUtc,
                lastShipError = status.LastShipError,
                heartbeatsProduced = status.HeartbeatsProduced,
                metricEventsProduced = status.MetricEventsProduced,
                eventLogEventsProduced = status.EventLogEventsProduced,
                serviceWatchEventsProduced = status.ServiceWatchEventsProduced,
                eventsShipped = status.EventsShipped,
                metricsEnabled = s.Policy.Metrics.Enabled,
                eventLogEnabled = s.Policy.EventLog.Enabled,
                serviceWatchEnabled = s.Policy.ServiceWatch.Enabled,
                includeTopProcesses = s.Policy.Metrics.IncludeTopProcesses,
                heartbeatIntervalSeconds = s.Policy.HeartbeatIntervalSeconds,
                eventLogPollIntervalSeconds = s.Policy.EventLog.PollIntervalSeconds,
                serviceWatchPollIntervalSeconds = s.Policy.ServiceWatch.PollIntervalSeconds,
                dataDirectory = config.ResolveDataDirectory(),
                recent = status.RecentNotes(),
                latestMetrics = status.LatestMetrics(),
                latestLogs = status.LatestLogEvents(15),
                topProcesses = status.TopProcesses()
            });
        });

        api.MapGet("/config", (IAgentConfigStore config) =>
        {
            var s = config.Current;
            return Results.Json(new
            {
                system = new
                {
                    s.System.CollectorBaseUrl,
                    apiKeyConfigured = !string.IsNullOrWhiteSpace(s.System.ApiKey),
                    s.System.HostId,
                    s.System.LocalUiHost,
                    s.System.LocalUiPort,
                    dataDirectory = config.ResolveDataDirectory()
                },
                policy = s.Policy
            });
        });

        api.MapPost("/config/system", async (SystemConfigUpdate update, IAgentConfigStore config) =>
        {
            var current = config.Current.System;
            if (!string.IsNullOrWhiteSpace(update.CollectorBaseUrl))
                current.CollectorBaseUrl = update.CollectorBaseUrl.Trim();
            if (update.ApiKey is not null)
                current.ApiKey = update.ApiKey;
            if (update.HostId is not null)
                current.HostId = update.HostId;
            await config.SaveSystemAsync(current);
            return Results.Ok(new { saved = true });
        });

        api.MapPost("/config/policy", async (PolicyConfig update, IAgentConfigStore config) =>
        {
            await config.SavePolicyAsync(update);
            return Results.Ok(new { saved = true });
        });

        api.MapGet("/queue", (IOutboundQueue queue) =>
        {
            var items = queue.Peek(100).Select(x => new
            {
                fileName = x.FileName,
                timestampUtc = x.Item.TimestampUtc,
                source = x.Item.Source,
                severity = x.Item.Severity,
                message = x.Item.Message,
                sourceProduct = x.Item.SourceProduct
            }).ToList();

            return Results.Json(new
            {
                count = queue.PendingCount,
                items
            });
        });

        api.MapGet("/sources", (IAgentConfigStore config, AgentRuntimeStatus status) =>
        {
            var policy = config.Current.Policy;
            var packages = DefaultEventLogPackages.Resolve(policy.EventLog)
                .Select(p => new
                {
                    p.Name,
                    p.Channel,
                    eventIds = p.EventIds,
                    enabled = policy.EventLog.Enabled
                });

            return Results.Json(new
            {
                metrics = new
                {
                    enabled = policy.Metrics.Enabled,
                    includeHostResources = policy.Metrics.IncludeHostResources,
                    includeTopProcesses = policy.Metrics.IncludeTopProcesses,
                    topProcessCount = policy.Metrics.TopProcessCount,
                    heartbeatIntervalSeconds = policy.HeartbeatIntervalSeconds,
                    lastHeartbeatUtc = status.LastHeartbeatUtc,
                    eventsProduced = status.MetricEventsProduced
                },
                eventLog = new
                {
                    enabled = policy.EventLog.Enabled,
                    pollIntervalSeconds = policy.EventLog.PollIntervalSeconds,
                    maxEventsPerPoll = policy.EventLog.MaxEventsPerPoll,
                    lastEventLogUtc = status.LastEventLogUtc,
                    lastError = status.LastEventLogError,
                    eventsProduced = status.EventLogEventsProduced,
                    packages
                },
                serviceWatch = new
                {
                    enabled = policy.ServiceWatch.Enabled,
                    pollIntervalSeconds = policy.ServiceWatch.PollIntervalSeconds,
                    lastServiceWatchUtc = status.LastServiceWatchUtc,
                    lastError = status.LastServiceWatchError,
                    eventsProduced = status.ServiceWatchEventsProduced,
                    configured = policy.ServiceWatch.Services,
                    snapshot = status.ServiceWatchSnapshot()
                },
                ship = new
                {
                    shipIntervalSeconds = policy.ShipIntervalSeconds,
                    maxEventsPerBatch = policy.MaxEventsPerBatch,
                    lastShipUtc = status.LastShipUtc,
                    lastShipSuccessUtc = status.LastShipSuccessUtc,
                    lastError = status.LastShipError,
                    eventsShipped = status.EventsShipped
                }
            });
        });

        api.MapGet("/events", (AgentRuntimeStatus status, string? direction, int? take) =>
        {
            var n = take ?? 100;
            var dir = (direction ?? "all").Trim().ToLowerInvariant();
            object items = dir switch
            {
                "produced" => status.RecentProduced(n),
                "shipped" => status.RecentShipped(n),
                _ => status.RecentProduced(n)
                    .Concat(status.RecentShipped(n))
                    .OrderByDescending(e => e.AtUtc)
                    .Take(n)
                    .ToArray()
            };
            return Results.Json(new { direction = dir, items });
        });

        api.MapDelete("/events", (AgentRuntimeStatus status) =>
        {
            status.ClearRecentEvents();
            return Results.Ok(new { cleared = true });
        });
    }
}

public sealed class SystemConfigUpdate
{
    public string? CollectorBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? HostId { get; set; }
}
