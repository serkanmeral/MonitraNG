using MngLogs.Agent;
using MngLogs.Agent.Configuration;
using MngLogs.Agent.Linux.Journal;
using MngLogs.Agent.LocalUi;
using MngLogs.Agent.Queue;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Linux.LocalUi;

/// <summary>
/// Local UI API shaped for shared MngLogs.UI (Windows parity).
/// Journal is exposed via eventLog* aliases so the Nuxt UI can render without a fork.
/// </summary>
public static class LinuxLocalUiEndpoints
{
    public static void MapLinuxLocalUi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/status", (IAgentConfigStore config, AgentRuntimeStatus status, IOutboundQueue queue) =>
        {
            var s = config.Current;
            var journal = s.Policy.Journal ?? new JournalPolicy();
            return Results.Json(new
            {
                service = "mnglogs-agent",
                platform = "linux",
                version = AgentVersion.Current,
                startedAtUtc = status.StartedAtUtc,
                hostId = config.ResolveHostId(),
                hostname = Environment.MachineName,
                domain = s.Policy.Domain,
                collectorBaseUrl = s.System.CollectorBaseUrl,
                collectorHealthy = status.CollectorHealthy,
                queuePending = queue.PendingCount,
                lastHeartbeatUtc = status.LastHeartbeatUtc,
                // Journal → eventLog aliases (shared UI)
                lastEventLogUtc = status.LastJournalUtc,
                lastEventLogError = status.LastJournalError,
                lastServiceWatchUtc = status.LastServiceWatchUtc,
                lastServiceWatchError = status.LastServiceWatchError,
                lastShipUtc = status.LastShipUtc,
                lastShipSuccessUtc = status.LastShipSuccessUtc,
                lastShipError = status.LastShipError,
                heartbeatsProduced = status.HeartbeatsProduced,
                metricEventsProduced = status.MetricEventsProduced,
                eventLogEventsProduced = status.JournalEventsProduced,
                journalEventsProduced = status.JournalEventsProduced,
                serviceWatchEventsProduced = status.ServiceWatchEventsProduced,
                eventsShipped = status.EventsShipped,
                metricsEnabled = s.Policy.Metrics.Enabled,
                eventLogEnabled = journal.Enabled,
                serviceWatchEnabled = s.Policy.ServiceWatch.Enabled,
                includeTopProcesses = s.Policy.Metrics.IncludeTopProcesses,
                heartbeatIntervalSeconds = s.Policy.HeartbeatIntervalSeconds,
                eventLogPollIntervalSeconds = journal.PollIntervalSeconds,
                serviceWatchPollIntervalSeconds = s.Policy.ServiceWatch.PollIntervalSeconds,
                dataDirectory = config.ResolveDataDirectory(),
                configDirectory = config.ResolveConfigDirectory(),
                hostInventory = status.HostInventory(),
                recent = status.RecentNotes(),
                latestMetrics = status.LatestMetrics(),
                latestLogs = status.LatestLogEvents(15),
                topProcesses = status.TopProcesses(),
                watchSnapshot = status.ServiceWatchSnapshot(),
                lastJournalUtc = status.LastJournalUtc,
                lastJournalError = status.LastJournalError
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
                    hostId = config.ResolveHostId(),
                    s.System.LocalUiHost,
                    s.System.LocalUiPort,
                    dataDirectory = config.ResolveDataDirectory(),
                    configDirectory = config.ResolveConfigDirectory()
                },
                policy = s.Policy,
                platform = "linux"
            });
        });

        api.MapGet("/auth/status", (HttpRequest req, ILocalUiPinAuth auth) =>
        {
            var token = req.Headers[LocalUiPinAuth.TokenHeaderName].FirstOrDefault();
            return Results.Json(auth.GetStatus(token));
        });

        api.MapPost("/auth/setup", (PinSetupRequest body, ILocalUiPinAuth auth) =>
        {
            var result = auth.Setup(body.Pin ?? "", body.PinConfirm ?? "");
            return result.Ok
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
        });

        api.MapPost("/auth/unlock", (PinUnlockRequest body, ILocalUiPinAuth auth) =>
        {
            var result = auth.Unlock(body.Pin ?? "");
            if (result.Ok)
                return Results.Ok(result);
            var code = result.LockedUntilUtc is not null
                ? StatusCodes.Status429TooManyRequests
                : StatusCodes.Status401Unauthorized;
            return Results.Json(result, statusCode: code);
        });

        api.MapPost("/auth/lock", (HttpRequest req, ILocalUiPinAuth auth) =>
        {
            var token = req.Headers[LocalUiPinAuth.TokenHeaderName].FirstOrDefault();
            auth.Lock(token);
            return Results.Ok(new { locked = true });
        });

        api.MapPost("/auth/change-pin", (HttpRequest req, PinChangeRequest body, ILocalUiPinAuth auth) =>
        {
            var token = req.Headers[LocalUiPinAuth.TokenHeaderName].FirstOrDefault();
            var result = auth.ChangePin(token, body.CurrentPin ?? "", body.NewPin ?? "", body.NewPinConfirm ?? "");
            return result.Ok
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
        });

        api.MapPost("/config/system", async (
            HttpRequest req,
            SystemConfigUpdate update,
            IAgentConfigStore config,
            ILocalUiPinAuth auth) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

            var system = config.Current.System;
            if (update.CollectorBaseUrl != null) system.CollectorBaseUrl = update.CollectorBaseUrl;
            if (update.ApiKey != null) system.ApiKey = update.ApiKey;
            if (update.HostId != null) system.HostId = update.HostId;
            if (update.LocalUiHost != null) system.LocalUiHost = update.LocalUiHost;
            if (update.LocalUiPort is int port) system.LocalUiPort = port;
            await config.SaveSystemAsync(system);
            return Results.Ok(new { saved = true });
        });

        api.MapPost("/config/policy", async (
            HttpRequest req,
            PolicyConfig update,
            IAgentConfigStore config,
            ILocalUiPinAuth auth) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

            await config.SavePolicyAsync(update);
            return Results.Ok(new { saved = true });
        });

        api.MapGet("/queue", (IOutboundQueue queue) =>
        {
            var items = queue.Peek(50).Select(x => new
            {
                fileName = x.FileName,
                timestampUtc = x.Item.TimestampUtc,
                source = x.Item.Source,
                severity = x.Item.Severity,
                message = x.Item.Message,
                sourceProduct = x.Item.SourceProduct
            });
            return Results.Json(new { count = queue.PendingCount, items });
        });

        api.MapGet("/sources", (IAgentConfigStore config, AgentRuntimeStatus status) =>
        {
            var policy = config.Current.Policy;
            var m = policy.Metrics;
            var w = policy.ServiceWatch;
            var j = policy.Journal ?? new JournalPolicy();
            var journalPackages = BuiltinJournalPackages.Resolve(j);

            var packages = journalPackages.Select(p => new
            {
                name = p.Name,
                channel = FormatJournalChannel(p),
                eventIds = Array.Empty<int>(),
                enabled = true,
                unit = p.Unit,
                identifier = p.Identifier,
                grep = p.Grep,
                priority = p.Priority
            }).ToArray();

            return Results.Json(new
            {
                readOnly = true,
                platform = "linux",
                producers = new object[]
                {
                    new
                    {
                        sourceType = "metric",
                        label = "Metrik",
                        enabled = m.Enabled,
                        intervalSeconds = policy.HeartbeatIntervalSeconds,
                        lastUtc = status.LastHeartbeatUtc,
                        eventsProduced = status.MetricEventsProduced,
                        lastError = (string?)null
                    },
                    new
                    {
                        sourceType = "linux-journal",
                        label = "Journal",
                        enabled = j.Enabled,
                        intervalSeconds = j.PollIntervalSeconds,
                        lastUtc = status.LastJournalUtc,
                        eventsProduced = status.JournalEventsProduced,
                        lastError = status.LastJournalError
                    },
                    new
                    {
                        sourceType = "service-watch",
                        label = "Servis izleme",
                        enabled = w.Enabled && w.Services.Count > 0,
                        intervalSeconds = w.PollIntervalSeconds,
                        lastUtc = status.LastServiceWatchUtc,
                        eventsProduced = status.ServiceWatchEventsProduced,
                        lastError = status.LastServiceWatchError
                    },
                    new
                    {
                        sourceType = "app-watch",
                        label = "Uygulama izleme",
                        enabled = w.Enabled && w.Applications.Count > 0,
                        intervalSeconds = w.PollIntervalSeconds,
                        lastUtc = status.LastServiceWatchUtc,
                        eventsProduced = status.ServiceWatchEventsProduced,
                        lastError = status.LastServiceWatchError
                    }
                },
                metrics = new
                {
                    enabled = m.Enabled,
                    includeHostResources = m.IncludeHostResources,
                    includeTopProcesses = m.IncludeTopProcesses,
                    topProcessCount = m.TopProcessCount,
                    heartbeatIntervalSeconds = policy.HeartbeatIntervalSeconds,
                    lastHeartbeatUtc = status.LastHeartbeatUtc,
                    eventsProduced = status.MetricEventsProduced,
                    definitions = Array.Empty<object>()
                },
                // Shared UI expects eventLog; map journal packages into that slot.
                eventLog = new
                {
                    enabled = j.Enabled,
                    pollIntervalSeconds = j.PollIntervalSeconds,
                    maxEventsPerPoll = j.MaxEventsPerPoll,
                    lastEventLogUtc = status.LastJournalUtc,
                    lastError = status.LastJournalError,
                    eventsProduced = status.JournalEventsProduced,
                    packages,
                    knownOptional = Array.Empty<object>()
                },
                journal = new
                {
                    enabled = j.Enabled,
                    pollIntervalSeconds = j.PollIntervalSeconds,
                    maxEventsPerPoll = j.MaxEventsPerPoll,
                    disabledPackages = j.DisabledPackages,
                    packages = journalPackages,
                    lastJournalUtc = status.LastJournalUtc,
                    lastError = status.LastJournalError,
                    eventsProduced = status.JournalEventsProduced
                },
                serviceWatch = new
                {
                    enabled = w.Enabled,
                    pollIntervalSeconds = w.PollIntervalSeconds,
                    restartCooldownSeconds = w.RestartCooldownSeconds,
                    restartMaxAttempts = w.RestartMaxAttempts,
                    includeInventory = w.IncludeInventory,
                    inventoryIntervalSeconds = w.InventoryIntervalSeconds,
                    lastServiceWatchUtc = status.LastServiceWatchUtc,
                    lastError = status.LastServiceWatchError,
                    eventsProduced = status.ServiceWatchEventsProduced,
                    configured = w.Services,
                    applications = w.Applications,
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

        api.MapGet("/watch", (AgentRuntimeStatus status) =>
            Results.Json(new
            {
                lastServiceWatchUtc = status.LastServiceWatchUtc,
                lastServiceWatchError = status.LastServiceWatchError,
                eventsProduced = status.ServiceWatchEventsProduced,
                targets = status.ServiceWatchSnapshot()
            }));

        api.MapGet("/events", (AgentRuntimeStatus status, string? direction, int? take, string? source) =>
        {
            var n = take ?? 100;
            var dir = (direction ?? "all").Trim().ToLowerInvariant();
            IEnumerable<RecentEventEntry> items = dir switch
            {
                "produced" => status.RecentProduced(n),
                "shipped" => status.RecentShipped(n),
                _ => status.RecentProduced(Math.Max(n, 80))
                    .Concat(status.RecentShipped(Math.Max(n, 80)))
                    .OrderByDescending(e => e.AtUtc)
                    .Take(n)
            };

            if (!string.IsNullOrWhiteSpace(source))
                items = items.Where(e => string.Equals(e.Source, source, StringComparison.OrdinalIgnoreCase));

            return Results.Json(new { direction = dir, items = items.Take(n).ToArray() });
        });

        api.MapDelete("/events", (AgentRuntimeStatus status) =>
        {
            status.ClearRecentEvents();
            return Results.Json(new { cleared = true });
        });
    }

    private static bool TryAuthorize(HttpRequest req, ILocalUiPinAuth auth, out IResult? denied)
    {
        var token = req.Headers[LocalUiPinAuth.TokenHeaderName].FirstOrDefault();
        if (!auth.TryValidateSession(token, out var error))
        {
            denied = Results.Json(new { ok = false, error }, statusCode: StatusCodes.Status401Unauthorized);
            return false;
        }

        denied = null;
        return true;
    }

    private static string FormatJournalChannel(JournalPackage p)
    {
        if (!string.IsNullOrWhiteSpace(p.Unit))
            return p.Unit!;
        if (!string.IsNullOrWhiteSpace(p.Identifier))
            return $"id:{p.Identifier}";
        if (!string.IsNullOrWhiteSpace(p.Priority))
            return $"prio:{p.Priority}";
        return "journal";
    }

    private sealed class PinSetupRequest
    {
        public string? Pin { get; set; }
        public string? PinConfirm { get; set; }
    }

    private sealed class PinUnlockRequest
    {
        public string? Pin { get; set; }
    }

    private sealed class PinChangeRequest
    {
        public string? CurrentPin { get; set; }
        public string? NewPin { get; set; }
        public string? NewPinConfirm { get; set; }
    }

    private sealed class SystemConfigUpdate
    {
        public string? CollectorBaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? HostId { get; set; }
        public string? LocalUiHost { get; set; }
        public int? LocalUiPort { get; set; }
    }
}
