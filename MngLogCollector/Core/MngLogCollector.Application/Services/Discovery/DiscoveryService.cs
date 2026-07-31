using Microsoft.Extensions.Logging;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Contracts.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Discovery;

public sealed class DiscoveryService : IDiscoveryService
{
    private readonly IKeeperDomainDirectoryReader _domains;
    private readonly IAdComputerDirectoryClient _ad;
    private readonly IDiscoveryHostStore _store;
    private readonly ILogger<DiscoveryService> _logger;

    public DiscoveryService(
        IKeeperDomainDirectoryReader domains,
        IAdComputerDirectoryClient ad,
        IDiscoveryHostStore store,
        ILogger<DiscoveryService> logger)
    {
        _domains = domains;
        _ad = ad;
        _store = store;
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

        return new DiscoveryHostListResponse
        {
            DomainId = domain.Name,
            Total = (int)total,
            Items = items.Select(MapHost).ToList()
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

    private static DiscoveryHostDto MapHost(DiscoveryHost h)
    {
        var hostname = !string.IsNullOrWhiteSpace(h.DisplayName)
            ? h.DisplayName!
            : !string.IsNullOrWhiteSpace(h.DnsHostName)
                ? h.DnsHostName!
                : h.SamAccountName.TrimEnd('$');

        var ip = h.IpAddresses.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

        return new DiscoveryHostDto
        {
            Id = h.Id,
            Hostname = hostname,
            Ip = ip,
            OsHint = h.OperatingSystem,
            Sources = h.Sources,
            SamAccountName = h.SamAccountName,
            LastSeenFromAd = h.LastSeenFromAd
        };
    }
}
