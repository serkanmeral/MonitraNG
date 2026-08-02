using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Discovery;

public sealed class DiscoveryService : IDiscoveryService
{
    private readonly IKeeperDomainDirectoryReader _domains;
    private readonly IAdComputerDirectoryClient _ad;
    private readonly IDiscoveryHostStore _store;
    private readonly IDiscoveryPrefixStore _prefixes;
    private readonly IDiscoveryScanJobStore _scanJobs;
    private readonly IDiscoveryScanQueue _scanQueue;
    private readonly IOptions<MngLogCollectorSettings> _settings;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(
        IKeeperDomainDirectoryReader domains,
        IAdComputerDirectoryClient ad,
        IDiscoveryHostStore store,
        IDiscoveryPrefixStore prefixes,
        IDiscoveryScanJobStore scanJobs,
        IDiscoveryScanQueue scanQueue,
        IOptions<MngLogCollectorSettings> settings,
        ILogger<DiscoveryService> logger)
    {
        _domains = domains;
        _ad = ad;
        _store = store;
        _prefixes = prefixes;
        _scanJobs = scanJobs;
        _scanQueue = scanQueue;
        _settings = settings;
        _logger = logger;
    }

    public async Task<DiscoveryHostListResponse> ListHostsAsync(
        string domainId,
        string? query,
        string? source,
        int limit,
        int offset,
        CancellationToken ct = default)
    {
        var domain = await ResolveDomainAsync(domainId, ct);
        await _store.EnsureIndexesAsync(domain.DatabaseName, ct);

        limit = Math.Clamp(limit <= 0 ? 500 : limit, 1, 2000);
        offset = Math.Max(0, offset);

        var (items, total) = await _store.ListAsync(
            domain.DatabaseName,
            domain.Name,
            query,
            source,
            limit,
            offset,
            ct);

        var (prefixEntries, _) = await ResolvePrefixEntriesAsync(domain, ct);
        var prefixDtos = ToPrefixDtos(prefixEntries);

        return new DiscoveryHostListResponse
        {
            DomainId = domain.Name,
            Total = (int)total,
            Items = items.Select(h => MapHost(h, prefixEntries)).ToList(),
            Prefixes = prefixDtos
        };
    }

    public async Task<DiscoverySummaryResponse> GetSummaryAsync(
        string domainId,
        CancellationToken ct = default)
    {
        var domain = await ResolveDomainAsync(domainId, ct);
        await _store.EnsureIndexesAsync(domain.DatabaseName, ct);

        var total = await _store.CountAsync(domain.DatabaseName, domain.Name, ct);
        var bySource = await _store.CountBySourceAsync(domain.DatabaseName, domain.Name, ct);
        var state = await _store.GetSyncStateAsync(domain.DatabaseName, "ad", ct);

        return new DiscoverySummaryResponse
        {
            DomainId = domain.Name,
            TotalHosts = (int)total,
            BySource = bySource,
            LastSyncAt = state?.LastSyncAt,
            LastSyncStatus = state?.LastSyncStatus ?? "never",
            LastSyncError = state?.LastSyncError,
            LastSyncStats = new DiscoverySyncStatsDto
            {
                Pulled = state?.Pulled ?? 0,
                Upserted = state?.Upserted ?? 0,
                DurationMs = state?.DurationMs ?? 0
            }
        };
    }

    public async Task<DiscoverySyncResponse> SyncAsync(
        DiscoverySyncRequest request,
        CancellationToken ct = default)
    {
        var source = string.IsNullOrWhiteSpace(request.Source) ? "ad" : request.Source.Trim().ToLowerInvariant();
        if (!string.Equals(source, "ad", StringComparison.Ordinal))
        {
            return new DiscoverySyncResponse
            {
                RunId = Guid.NewGuid().ToString("N"),
                Status = "error",
                Error = $"Unsupported discovery source '{source}'. MVP supports 'ad' only."
            };
        }

        var runId = Guid.NewGuid().ToString("N");
        var swAll = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<DiscoverySyncDomainResult>();

        IReadOnlyList<DiscoveryDomainInfo> targets;
        if (!string.IsNullOrWhiteSpace(request.DomainId))
        {
            var one = await ResolveDomainAsync(request.DomainId, ct);
            targets = [one];
        }
        else
        {
            targets = await _domains.GetActiveDomainsWithLdapAsync(ct);
        }

        if (targets.Count == 0)
        {
            swAll.Stop();
            return new DiscoverySyncResponse
            {
                RunId = runId,
                Status = "error",
                DurationMs = swAll.ElapsedMilliseconds,
                Error = "No domains with directoryLdap enabled.",
                Domains = results
            };
        }

        foreach (var domain in targets)
        {
            results.Add(await SyncDomainAdAsync(domain, runId, ct));
        }

        swAll.Stop();
        var anyError = results.Any(r => r.Status != "ok");
        return new DiscoverySyncResponse
        {
            RunId = runId,
            Status = anyError ? (results.All(r => r.Status != "ok") ? "error" : "partial") : "ok",
            Pulled = results.Sum(r => r.Pulled),
            Upserted = results.Sum(r => r.Upserted),
            DurationMs = swAll.ElapsedMilliseconds,
            Error = anyError
                ? string.Join("; ", results.Where(r => r.Error != null).Select(r => $"{r.DomainId}: {r.Error}"))
                : null,
            Domains = results
        };
    }

    private async Task<DiscoverySyncDomainResult> SyncDomainAdAsync(
        DiscoveryDomainInfo domain,
        string runId,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ldap = domain.DirectoryLdap;
        if (ldap is null || !ldap.Enabled)
        {
            sw.Stop();
            return new DiscoverySyncDomainResult
            {
                DomainId = domain.Name,
                Status = "error",
                DurationMs = sw.ElapsedMilliseconds,
                Error = "directoryLdap is missing or disabled."
            };
        }

        if (string.IsNullOrWhiteSpace(ldap.Host) || string.IsNullOrWhiteSpace(ldap.BaseDn))
        {
            sw.Stop();
            return new DiscoverySyncDomainResult
            {
                DomainId = domain.Name,
                Status = "error",
                DurationMs = sw.ElapsedMilliseconds,
                Error = "directoryLdap.host and baseDn are required."
            };
        }

        try
        {
            await _store.EnsureIndexesAsync(domain.DatabaseName, ct);
            var computers = await _ad.SearchComputersAsync(ldap, ct);
            var now = DateTime.UtcNow;
            var hosts = computers
                .Where(c => !string.IsNullOrWhiteSpace(c.SamAccountName))
                .Select(c => MapComputer(domain.Name, c, now, runId))
                .ToList();

            await _store.UpsertManyAsync(domain.DatabaseName, hosts, ct);
            sw.Stop();

            var state = new DiscoverySyncState
            {
                Id = "ad",
                DomainId = domain.Name,
                LastSyncAt = now,
                LastSyncStatus = "ok",
                LastSyncError = null,
                LastSyncRunId = runId,
                Pulled = hosts.Count,
                Upserted = hosts.Count,
                DurationMs = sw.ElapsedMilliseconds
            };
            await _store.SaveSyncStateAsync(domain.DatabaseName, state, ct);

            _logger.LogInformation(
                "Discovery AD sync ok domain={Domain} pulled={Pulled} ms={Ms}",
                domain.Name, hosts.Count, sw.ElapsedMilliseconds);

            return new DiscoverySyncDomainResult
            {
                DomainId = domain.Name,
                Status = "ok",
                Pulled = hosts.Count,
                Upserted = hosts.Count,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Discovery AD sync failed domain={Domain}", domain.Name);

            try
            {
                await _store.SaveSyncStateAsync(domain.DatabaseName, new DiscoverySyncState
                {
                    Id = "ad",
                    DomainId = domain.Name,
                    LastSyncAt = DateTime.UtcNow,
                    LastSyncStatus = "error",
                    LastSyncError = ex.Message,
                    LastSyncRunId = runId,
                    Pulled = 0,
                    Upserted = 0,
                    DurationMs = sw.ElapsedMilliseconds
                }, ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "Failed to persist sync error state for {Domain}", domain.Name);
            }

            return new DiscoverySyncDomainResult
            {
                DomainId = domain.Name,
                Status = "error",
                DurationMs = sw.ElapsedMilliseconds,
                Error = ex.Message
            };
        }
    }

    private async Task<DiscoveryDomainInfo> ResolveDomainAsync(string domainId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("domainId is required.", nameof(domainId));

        var domain = await _domains.GetByNameOrIdAsync(domainId.Trim(), ct);
        if (domain is null)
            throw new InvalidOperationException($"Domain '{domainId}' not found.");

        return domain;
    }

    private static DiscoveryHost MapComputer(
        string domainName,
        AdComputerRecord c,
        DateTime now,
        string runId)
    {
        var sam = c.SamAccountName.Trim();
        var hostname = !string.IsNullOrWhiteSpace(c.DnsHostName)
            ? c.DnsHostName.Trim()
            : sam.TrimEnd('$');

        return new DiscoveryHost
        {
            DomainId = domainName,
            ObjectGuid = string.IsNullOrWhiteSpace(c.ObjectGuid) ? $"ad:{sam}" : c.ObjectGuid,
            SamAccountName = sam,
            DnsHostName = string.IsNullOrWhiteSpace(c.DnsHostName) ? null : c.DnsHostName.Trim(),
            DisplayName = hostname,
            IpAddresses = [],
            OperatingSystem = c.OperatingSystem,
            OperatingSystemVersion = c.OperatingSystemVersion,
            DistinguishedName = c.DistinguishedName,
            Sources = ["ad"],
            LastSeenFromAd = now,
            AdEnabled = c.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            LastSyncRunId = runId
        };
    }

    public async Task<DiscoveryScanStartResponse> StartScanAsync(
        DiscoveryScanStartRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DomainId))
        {
            return new DiscoveryScanStartResponse
            {
                Status = "error",
                Error = "domainId is required."
            };
        }

        if (!NetworkCidrParser.TryParse(request.Cidr, out var targets, out var parseError))
        {
            return new DiscoveryScanStartResponse
            {
                Status = "error",
                Error = parseError
            };
        }

        var domain = await ResolveDomainAsync(request.DomainId, ct);
        await _store.EnsureIndexesAsync(domain.DatabaseName, ct);
        await _scanJobs.EnsureIndexesAsync(domain.DatabaseName, ct);

        var active = await _scanJobs.FindActiveAsync(domain.DatabaseName, domain.Name, ct);
        if (active is not null)
        {
            return new DiscoveryScanStartResponse
            {
                RunId = active.Id,
                Status = "error",
                TotalTargets = active.TotalTargets,
                Error = $"A scan is already {active.Status} (runId={active.Id}). Cancel it or wait."
            };
        }

        var runId = Guid.NewGuid().ToString("N");
        var job = new DiscoveryScanJob
        {
            Id = runId,
            DomainId = domain.Name,
            DatabaseName = domain.DatabaseName,
            Cidr = request.Cidr.Trim(),
            EnrichWithAd = request.EnrichWithAd,
            Status = "queued",
            TotalTargets = targets.Count,
            CreatedAt = DateTime.UtcNow
        };

        await _scanJobs.InsertAsync(domain.DatabaseName, job, ct);
        await _scanQueue.EnqueueAsync(domain.DatabaseName, runId, ct);

        _logger.LogInformation(
            "Discovery scan queued run={RunId} domain={Domain} cidr={Cidr} targets={N} enrichAd={Ad}",
            runId, domain.Name, job.Cidr, targets.Count, job.EnrichWithAd);

        return new DiscoveryScanStartResponse
        {
            RunId = runId,
            Status = "queued",
            TotalTargets = targets.Count
        };
    }

    public async Task<DiscoveryScanStatusResponse?> GetScanAsync(
        string domainId,
        string runId,
        CancellationToken ct = default)
    {
        var domain = await ResolveDomainAsync(domainId, ct);
        var job = await _scanJobs.GetAsync(domain.DatabaseName, runId, ct);
        return job is null ? null : MapScan(job);
    }

    public async Task<DiscoveryScanStatusResponse?> CancelScanAsync(
        string domainId,
        string runId,
        CancellationToken ct = default)
    {
        var domain = await ResolveDomainAsync(domainId, ct);
        var job = await _scanJobs.GetAsync(domain.DatabaseName, runId, ct);
        if (job is null)
            return null;

        if (job.Status is "completed" or "failed" or "cancelled")
            return MapScan(job);

        job.CancelRequested = true;
        if (job.Status == "queued")
        {
            job.Status = "cancelled";
            job.CompletedAt = DateTime.UtcNow;
        }

        await _scanJobs.UpdateAsync(domain.DatabaseName, job, ct);
        return MapScan(job);
    }

    public async Task<DiscoveryClearResponse> ClearHostsAsync(
        DiscoveryClearRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.DomainId))
        {
            return new DiscoveryClearResponse
            {
                Status = "error",
                Error = "domainId is required."
            };
        }

        var source = string.IsNullOrWhiteSpace(request.Source)
            ? null
            : request.Source.Trim().ToLowerInvariant();
        if (source is not null && source is not ("scan" or "ad"))
        {
            return new DiscoveryClearResponse
            {
                DomainId = request.DomainId.Trim(),
                Status = "error",
                Source = source,
                Error = "source must be empty (all), 'scan', or 'ad'."
            };
        }

        var domain = await ResolveDomainAsync(request.DomainId, ct);
        await _store.EnsureIndexesAsync(domain.DatabaseName, ct);
        await _scanJobs.EnsureIndexesAsync(domain.DatabaseName, ct);

        var active = await _scanJobs.FindActiveAsync(domain.DatabaseName, domain.Name, ct);
        if (active is not null)
        {
            return new DiscoveryClearResponse
            {
                DomainId = domain.Name,
                Status = "error",
                Source = source,
                Error = $"A scan is already {active.Status} (runId={active.Id}). Cancel it or wait."
            };
        }

        var deleted = await _store.DeleteAsync(domain.DatabaseName, domain.Name, source, ct);
        _logger.LogInformation(
            "Discovery hosts cleared domain={Domain} source={Source} deleted={Deleted}",
            domain.Name,
            source ?? "all",
            deleted);

        return new DiscoveryClearResponse
        {
            DomainId = domain.Name,
            Status = "ok",
            Source = source,
            Deleted = deleted
        };
    }

    private static DiscoveryScanStatusResponse MapScan(DiscoveryScanJob job) => new()
    {
        RunId = job.Id,
        DomainId = job.DomainId,
        Cidr = job.Cidr,
        EnrichWithAd = job.EnrichWithAd,
        Status = job.Status,
        ProgressPercent = job.ProgressPercent,
        TotalTargets = job.TotalTargets,
        Probed = job.Probed,
        FoundAlive = job.FoundAlive,
        FoundWindows = job.FoundWindows,
        FoundLinux = job.FoundLinux,
        FoundUnknown = job.FoundUnknown,
        Upserted = job.Upserted,
        Error = job.Error,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt
    };

    public async Task<DiscoveryPrefixesResponse> GetPrefixesAsync(
        string domainId,
        CancellationToken ct = default)
    {
        var domain = await ResolveDomainAsync(domainId, ct);
        var (entries, source) = await ResolvePrefixEntriesAsync(domain, ct);
        return new DiscoveryPrefixesResponse
        {
            DomainId = domain.Name,
            Source = source,
            Prefixes = ToPrefixDtos(entries)
        };
    }

    public async Task<DiscoveryPrefixesResponse> PutPrefixesAsync(
        string domainId,
        DiscoveryPrefixesPutRequest request,
        CancellationToken ct = default)
    {
        var domain = await ResolveDomainAsync(domainId, ct);
        var normalized = new List<DiscoveryPrefixEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in request.Prefixes ?? [])
        {
            if (raw is null || string.IsNullOrWhiteSpace(raw.Cidr))
                continue;
            var cidr = raw.Cidr.Trim();
            if (!PrefixTableMatcher.TryParseCidr(cidr, out var network, out var prefixLen))
                throw new InvalidOperationException($"Invalid IPv4 CIDR: {cidr}");

            var normalizedCidr = $"{(network >> 24) & 0xFF}.{(network >> 16) & 0xFF}.{(network >> 8) & 0xFF}.{network & 0xFF}/{prefixLen}";
            if (!seen.Add(normalizedCidr))
                continue;

            var label = string.IsNullOrWhiteSpace(raw.Label) ? normalizedCidr : raw.Label.Trim();
            normalized.Add(new DiscoveryPrefixEntry
            {
                Cidr = normalizedCidr,
                Label = label,
                VlanName = string.IsNullOrWhiteSpace(raw.VlanName) ? null : raw.VlanName.Trim()
            });
        }

        await _prefixes.UpsertAsync(
            domain.DatabaseName,
            new DiscoveryPrefixTableDocument
            {
                DomainId = domain.Name,
                Prefixes = normalized.Select(p => new DiscoveryPrefixRow
                {
                    Cidr = p.Cidr,
                    Label = p.Label,
                    VlanName = p.VlanName
                }).ToList(),
                UpdatedAt = DateTime.UtcNow
            },
            ct);

        return new DiscoveryPrefixesResponse
        {
            DomainId = domain.Name,
            Source = "mongo",
            Prefixes = ToPrefixDtos(normalized)
        };
    }

    private async Task<(IReadOnlyList<DiscoveryPrefixEntry> Entries, string Source)> ResolvePrefixEntriesAsync(
        DiscoveryDomainInfo domain,
        CancellationToken ct)
    {
        var doc = await _prefixes.GetAsync(domain.DatabaseName, domain.Name, ct);
        if (doc?.Prefixes is { Count: > 0 })
        {
            var fromMongo = doc.Prefixes
                .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Cidr))
                .Select(p => new DiscoveryPrefixEntry
                {
                    Cidr = p.Cidr.Trim(),
                    Label = string.IsNullOrWhiteSpace(p.Label) ? p.Cidr.Trim() : p.Label.Trim(),
                    VlanName = string.IsNullOrWhiteSpace(p.VlanName) ? null : p.VlanName.Trim()
                })
                .ToList();
            if (fromMongo.Count > 0)
                return (fromMongo, "mongo");
        }

        var fromConfig = (_settings.Value.Discovery?.Prefixes ?? [])
            .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Cidr))
            .Select(p => new DiscoveryPrefixEntry
            {
                Cidr = p.Cidr.Trim(),
                Label = string.IsNullOrWhiteSpace(p.Label) ? p.Cidr.Trim() : p.Label.Trim(),
                VlanName = string.IsNullOrWhiteSpace(p.VlanName) ? null : p.VlanName.Trim()
            })
            .ToList();
        return (fromConfig, "config");
    }

    private static IReadOnlyList<DiscoveryPrefixDto> ToPrefixDtos(IEnumerable<DiscoveryPrefixEntry> entries) =>
        entries.Select(p => new DiscoveryPrefixDto
        {
            Cidr = p.Cidr,
            Label = p.Label,
            VlanName = p.VlanName
        }).ToList();

    private static DiscoveryHostDto MapHost(DiscoveryHost h, IReadOnlyList<DiscoveryPrefixEntry> prefixes)
    {
        var hostname = !string.IsNullOrWhiteSpace(h.DisplayName)
            ? h.DisplayName!
            : !string.IsNullOrWhiteSpace(h.DnsHostName)
                ? h.DnsHostName!
                : h.SamAccountName.TrimEnd('$');

        var ip = h.IpAddresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        var osHint = !string.IsNullOrWhiteSpace(h.OsFamilyHint)
            ? h.OsFamilyHint
            : h.OperatingSystem;

        // Fresh LPM so prefix-table edits apply without a rescan.
        PrefixTableMatcher.ApplyToHost(h, prefixes);

        return new DiscoveryHostDto
        {
            Id = h.Id,
            Hostname = hostname,
            Ip = ip,
            OsHint = osHint,
            OpenPorts = h.OpenPorts ?? [],
            DeviceRoleHint = h.DeviceRoleHint,
            IdentityConfidence = h.IdentityConfidence,
            IdentitySummary = h.IdentitySummary,
            HttpTitle = h.HttpTitle,
            TlsCommonName = h.TlsCommonName,
            SshBanner = h.SshBanner,
            SubnetCidr = h.SubnetCidr,
            SiteLabel = h.SiteLabel,
            VlanName = h.VlanName,
            Sources = h.Sources,
            SamAccountName = h.SamAccountName,
            LastSeenFromAd = h.LastSeenFromAd,
            LastSeenFromScan = h.LastSeenFromScan
        };
    }
}
