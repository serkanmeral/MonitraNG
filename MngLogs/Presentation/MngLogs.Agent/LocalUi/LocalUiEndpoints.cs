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
                topProcesses = status.TopProcesses(),
                watchSnapshot = status.ServiceWatchSnapshot()
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
                    dataDirectory = config.ResolveDataDirectory()
                },
                policy = s.Policy
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

        api.MapPost("/config/system", async (HttpRequest req, SystemConfigUpdate update, IAgentConfigStore config, ILocalUiPinAuth auth) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

            var current = config.Current.System;
            if (!string.IsNullOrWhiteSpace(update.CollectorBaseUrl))
                current.CollectorBaseUrl = update.CollectorBaseUrl.Trim();
            if (update.ApiKey is not null)
                current.ApiKey = update.ApiKey;
            if (update.HostId is not null)
            {
                var trimmed = update.HostId.Trim();
                current.HostId = string.IsNullOrWhiteSpace(trimmed)
                    ? Environment.MachineName
                    : trimmed;
            }
            await config.SaveSystemAsync(current);
            return Results.Ok(new { saved = true });
        });

        api.MapPost("/config/policy", async (HttpRequest req, PolicyConfig update, IAgentConfigStore config, ILocalUiPinAuth auth) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

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

        api.MapGet("/sources", (IAgentConfigStore config, AgentRuntimeStatus status, IEventLogPackageCatalogStore catalog) =>
        {
            var policy = config.Current.Policy;
            var packages = DefaultEventLogPackages.Resolve(policy.EventLog, catalog.ServerPackages)
                .Select(p => new
                {
                    p.Name,
                    p.Channel,
                    eventIds = p.EventIds,
                    enabled = policy.EventLog.Enabled
                });

            var metricDefinitions = BuildMetricDefinitions(policy);

            return Results.Json(new
            {
                readOnly = true,
                producers = new object[]
                {
                    new
                    {
                        sourceType = "metric",
                        label = "Metrik",
                        enabled = policy.Metrics.Enabled,
                        intervalSeconds = policy.HeartbeatIntervalSeconds,
                        lastUtc = status.LastHeartbeatUtc,
                        eventsProduced = status.MetricEventsProduced
                    },
                    new
                    {
                        sourceType = "windows-eventlog",
                        label = "Olay günlüğü",
                        enabled = policy.EventLog.Enabled,
                        intervalSeconds = policy.EventLog.PollIntervalSeconds,
                        lastUtc = status.LastEventLogUtc,
                        eventsProduced = status.EventLogEventsProduced,
                        lastError = status.LastEventLogError
                    },
                    new
                    {
                        sourceType = "service-watch",
                        label = "Servis izleme",
                        enabled = policy.ServiceWatch.Enabled && policy.ServiceWatch.Services.Count > 0,
                        intervalSeconds = policy.ServiceWatch.PollIntervalSeconds,
                        lastUtc = status.LastServiceWatchUtc,
                        eventsProduced = status.ServiceWatchEventsProduced,
                        lastError = status.LastServiceWatchError
                    },
                    new
                    {
                        sourceType = "app-watch",
                        label = "Uygulama izleme",
                        enabled = policy.ServiceWatch.Enabled && policy.ServiceWatch.Applications.Count > 0,
                        intervalSeconds = policy.ServiceWatch.PollIntervalSeconds,
                        lastUtc = status.LastServiceWatchUtc,
                        eventsProduced = status.ServiceWatchEventsProduced,
                        lastError = status.LastServiceWatchError
                    }
                },
                metrics = new
                {
                    enabled = policy.Metrics.Enabled,
                    includeHostResources = policy.Metrics.IncludeHostResources,
                    includeTopProcesses = policy.Metrics.IncludeTopProcesses,
                    topProcessCount = policy.Metrics.TopProcessCount,
                    heartbeatIntervalSeconds = policy.HeartbeatIntervalSeconds,
                    lastHeartbeatUtc = status.LastHeartbeatUtc,
                    eventsProduced = status.MetricEventsProduced,
                    definitions = metricDefinitions
                },
                eventLog = new
                {
                    enabled = policy.EventLog.Enabled,
                    pollIntervalSeconds = policy.EventLog.PollIntervalSeconds,
                    maxEventsPerPoll = policy.EventLog.MaxEventsPerPoll,
                    lastEventLogUtc = status.LastEventLogUtc,
                    lastError = status.LastEventLogError,
                    eventsProduced = status.EventLogEventsProduced,
                    packages,
                    knownOptional = new[]
                    {
                        new
                        {
                            DefaultEventLogPackages.SecurityAuth.Name,
                            DefaultEventLogPackages.SecurityAuth.Channel,
                            eventIds = DefaultEventLogPackages.SecurityAuth.EventIds,
                            requiresElevation = true
                        }
                    }
                },
                serviceWatch = new
                {
                    enabled = policy.ServiceWatch.Enabled,
                    pollIntervalSeconds = policy.ServiceWatch.PollIntervalSeconds,
                    restartCooldownSeconds = policy.ServiceWatch.RestartCooldownSeconds,
                    restartMaxAttempts = policy.ServiceWatch.RestartMaxAttempts,
                    includeInventory = policy.ServiceWatch.IncludeInventory,
                    inventoryIntervalSeconds = policy.ServiceWatch.InventoryIntervalSeconds,
                    lastServiceWatchUtc = status.LastServiceWatchUtc,
                    lastError = status.LastServiceWatchError,
                    eventsProduced = status.ServiceWatchEventsProduced,
                    configured = policy.ServiceWatch.Services,
                    applications = policy.ServiceWatch.Applications,
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

        api.MapGet("/eventlog/known-packages", (IEventLogPackageCatalogStore catalog) =>
        {
            static object MapPkg(EventLogPackage p, bool optional) => new
            {
                name = p.Name,
                channel = p.Channel,
                eventIds = p.EventIds,
                optional
            };

            var defaults = catalog.ServerPackages.Select(p => MapPkg(p, false)).ToArray();
            var optional = catalog.OptionalPackages.Select(p => MapPkg(p, true)).ToArray();
            return Results.Json(new
            {
                source = catalog.Source,
                lastSyncedUtc = catalog.LastSyncedUtc,
                defaults,
                optional,
                all = defaults.Concat(optional).ToArray()
            });
        });

        api.MapGet("/eventlog/package-plan", (IAgentConfigStore config, IEventLogPackageCatalogStore catalog) =>
        {
            var el = config.Current.Policy.EventLog;
            var server = catalog.ServerPackages;
            var effective = DefaultEventLogPackages.Resolve(el, server);
            var legacyMode = el.Packages is { Count: > 0 }
                && (el.AgentOverrides is not { Count: > 0 })
                && (el.DisabledServerPackages is not { Count: > 0 });

            return Results.Json(new
            {
                source = catalog.Source,
                lastSyncedUtc = catalog.LastSyncedUtc,
                syncIntervalSeconds = el.PackageCatalogSyncIntervalSeconds,
                legacyMode,
                server = server.Select(p => new
                {
                    p.Name,
                    p.Channel,
                    eventIds = p.EventIds,
                    disabled = el.DisabledServerPackages.Any(d =>
                        string.Equals(d, p.Name, StringComparison.OrdinalIgnoreCase))
                }),
                optional = catalog.OptionalPackages.Select(p => new
                {
                    p.Name,
                    p.Channel,
                    eventIds = p.EventIds,
                    optional = true
                }),
                agentOverrides = el.AgentOverrides,
                disabledServerPackages = el.DisabledServerPackages,
                effective = effective.Select(p => new { p.Name, p.Channel, eventIds = p.EventIds })
            });
        });

        api.MapPost("/eventlog/sync-catalog", async (HttpRequest req, ILocalUiPinAuth auth, IEventLogPackageCatalogStore catalog) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

            await catalog.RefreshAsync(req.HttpContext.RequestAborted);
            return Results.Ok(new
            {
                synced = true,
                source = catalog.Source,
                lastSyncedUtc = catalog.LastSyncedUtc,
                count = catalog.ServerPackages.Count
            });
        });

        api.MapGet("/host/services", (HttpRequest req, ILocalUiPinAuth auth) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

            try
            {
                var items = HostLocalInventory.ListWindowsServices()
                    .Select(s => new { name = s.Name, displayName = s.DisplayName, status = s.Status })
                    .ToArray();
                return Results.Json(new { items, count = items.Length });
            }
            catch (Exception ex)
            {
                return Results.Json(new { items = Array.Empty<object>(), count = 0, error = ex.Message });
            }
        });

        api.MapPost("/host/browse-executable", (HttpRequest req, ILocalUiPinAuth auth, CancellationToken ct) =>
        {
            if (!TryAuthorize(req, auth, out var denied))
                return denied!;

            try
            {
                var path = HostLocalInventory.BrowseExecutable(ct);
                if (string.IsNullOrWhiteSpace(path))
                    return Results.Json(new { cancelled = true });

                var full = Path.GetFullPath(path);
                return Results.Json(new
                {
                    cancelled = false,
                    path = full,
                    processName = Path.GetFileNameWithoutExtension(full),
                    fileName = Path.GetFileName(full),
                    directory = Path.GetDirectoryName(full)
                });
            }
            catch (OperationCanceledException)
            {
                return Results.Json(new { cancelled = true });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    cancelled = true,
                    error = ex.Message
                });
            }
        });
    }

    private static bool TryAuthorize(HttpRequest req, ILocalUiPinAuth auth, out IResult? denied)
    {
        var token = req.Headers[LocalUiPinAuth.TokenHeaderName].FirstOrDefault();
        if (auth.TryValidateSession(token, out var error))
        {
            denied = null;
            return true;
        }

        denied = Results.Json(new { ok = false, error }, statusCode: StatusCodes.Status401Unauthorized);
        return false;
    }

    private static IReadOnlyList<object> BuildMetricDefinitions(PolicyConfig policy)
    {
        var list = new List<object>();
        if (!policy.Metrics.Enabled)
            return list;

        list.Add(new
        {
            name = "up",
            metric = "up",
            action = "host.up",
            sourceType = "metric",
            description = "Host heartbeat (alive)",
            intervalSeconds = policy.HeartbeatIntervalSeconds,
            enabled = true
        });

        if (policy.Metrics.IncludeHostResources)
        {
            list.Add(new
            {
                name = "cpu.percent",
                metric = "cpu.percent",
                action = "host.cpu",
                sourceType = "metric",
                description = "CPU kullanımı (%)",
                intervalSeconds = policy.HeartbeatIntervalSeconds,
                enabled = true
            });
            list.Add(new
            {
                name = "memory",
                metric = "memory.*",
                action = "host.memory",
                sourceType = "metric",
                description = "Bellek kullanımı",
                intervalSeconds = policy.HeartbeatIntervalSeconds,
                enabled = true
            });
            list.Add(new
            {
                name = "disk",
                metric = "disk.*",
                action = "host.disk",
                sourceType = "metric",
                description = "Disk kullanımı (sürücü bazlı)",
                intervalSeconds = policy.HeartbeatIntervalSeconds,
                enabled = true
            });
        }

        if (policy.Metrics.IncludeTopProcesses)
        {
            list.Add(new
            {
                name = "process.top_cpu",
                metric = "process.top_cpu",
                action = "process.top_cpu",
                sourceType = "metric",
                description = $"CPU Top-{policy.Metrics.TopProcessCount} süreç özeti",
                intervalSeconds = policy.HeartbeatIntervalSeconds,
                enabled = true
            });
            list.Add(new
            {
                name = "process.top_memory",
                metric = "process.top_memory",
                action = "process.top_memory",
                sourceType = "metric",
                description = $"Bellek Top-{policy.Metrics.TopProcessCount} süreç özeti",
                intervalSeconds = policy.HeartbeatIntervalSeconds,
                enabled = true
            });
        }

        if (policy.ServiceWatch.Enabled && policy.ServiceWatch.IncludeInventory)
        {
            list.Add(new
            {
                name = "watch.inventory",
                metric = "watch.inventory",
                action = "watch.inventory",
                sourceType = "metric",
                description = "İzlenen servis/uygulama envanter özeti",
                intervalSeconds = policy.ServiceWatch.InventoryIntervalSeconds,
                enabled = true
            });
        }

        return list;
    }
}

public sealed class SystemConfigUpdate
{
    public string? CollectorBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? HostId { get; set; }
}

public sealed class PinSetupRequest
{
    public string? Pin { get; set; }
    public string? PinConfirm { get; set; }
}

public sealed class PinUnlockRequest
{
    public string? Pin { get; set; }
}

public sealed class PinChangeRequest
{
    public string? CurrentPin { get; set; }
    public string? NewPin { get; set; }
    public string? NewPinConfirm { get; set; }
}
