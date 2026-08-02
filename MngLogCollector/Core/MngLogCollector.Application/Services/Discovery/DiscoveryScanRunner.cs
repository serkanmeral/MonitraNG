using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Discovery;

/// <summary>Executes one network scan job (TCP port profile + optional AD enrich).</summary>
public sealed class DiscoveryScanRunner
{
    private const int MaxConcurrency = 64;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan FingerprintTimeout = TimeSpan.FromMilliseconds(800);

    private readonly IDiscoveryScanJobStore _jobs;
    private readonly IDiscoveryHostStore _hosts;
    private readonly IDiscoveryPrefixStore _prefixes;
    private readonly IOptions<MngLogCollectorSettings> _settings;
    private readonly ILogger<DiscoveryScanRunner> _logger;

    public DiscoveryScanRunner(
        IDiscoveryScanJobStore jobs,
        IDiscoveryHostStore hosts,
        IDiscoveryPrefixStore prefixes,
        IOptions<MngLogCollectorSettings> settings,
        ILogger<DiscoveryScanRunner> logger)
    {
        _jobs = jobs;
        _hosts = hosts;
        _prefixes = prefixes;
        _settings = settings;
        _logger = logger;
    }

    public async Task RunAsync(string databaseName, string runId, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(databaseName, runId, ct);
        if (job is null)
        {
            _logger.LogWarning("Scan job {RunId} not found in {Db}", runId, databaseName);
            return;
        }

        if (job.Status is "completed" or "failed" or "cancelled")
            return;

        if (!NetworkCidrParser.TryParse(job.Cidr, out var targets, out var parseError))
        {
            job.Status = "failed";
            job.Error = parseError;
            job.CompletedAt = DateTime.UtcNow;
            await _jobs.UpdateAsync(databaseName, job, ct);
            return;
        }

        job.Status = "running";
        job.StartedAt = DateTime.UtcNow;
        job.TotalTargets = targets.Count;
        job.ProgressPercent = 0;
        await _jobs.UpdateAsync(databaseName, job, ct);

        Dictionary<string, DiscoveryHost>? adByIp = null;
        if (job.EnrichWithAd)
        {
            adByIp = await BuildAdIpIndexAsync(databaseName, job.DomainId, ct);
        }

        var alive = new ConcurrentBag<(
            string Ip,
            IReadOnlyList<int> Ports,
            string PortOs,
            ServiceFingerprintSignals Signals,
            IdentityClassification Identity)>();
        var probed = 0;
        var gate = new SemaphoreSlim(MaxConcurrency);
        var tasks = new List<Task>(targets.Count);

        try
        {
            foreach (var ip in targets)
            {
                if (job.CancelRequested || ct.IsCancellationRequested)
                    break;

                await gate.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (Volatile.Read(ref probed) >= 0)
                        {
                            var latest = await _jobs.GetAsync(databaseName, runId, CancellationToken.None);
                            if (latest?.CancelRequested == true)
                                return;
                        }

                        var result = await TcpPortProbe.ProbeAsync(ip, ConnectTimeout, ct);
                        if (result.Alive)
                        {
                            var signals = await ServiceFingerprintProbe.ProbeAsync(
                                ip, result.OpenPorts, FingerprintTimeout, CancellationToken.None);
                            var identity = IdentityClassifier.Classify(
                                result.OpenPorts, result.OsFamilyHint, signals);
                            alive.Add((result.Ip, result.OpenPorts, result.OsFamilyHint, signals, identity));
                        }

                        var n = Interlocked.Increment(ref probed);
                        if (n % 8 == 0 || n == targets.Count)
                        {
                            job.Probed = n;
                            job.FoundAlive = alive.Count;
                            job.ProgressPercent = (int)Math.Clamp(100.0 * n / targets.Count, 0, 99);
                            await _jobs.UpdateAsync(databaseName, job, CancellationToken.None);
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // fall through to finalize
        }

        job = await _jobs.GetAsync(databaseName, runId, CancellationToken.None) ?? job;
        if (job.CancelRequested || ct.IsCancellationRequested)
        {
            job.Status = "cancelled";
            job.Probed = probed;
            job.FoundAlive = alive.Count;
            job.ProgressPercent = (int)Math.Clamp(100.0 * probed / Math.Max(1, targets.Count), 0, 100);
            job.CompletedAt = DateTime.UtcNow;
            await _jobs.UpdateAsync(databaseName, job, CancellationToken.None);
            return;
        }

        var now = DateTime.UtcNow;
        var upserts = new List<DiscoveryHost>();
        var win = 0;
        var lin = 0;
        var unk = 0;

        var prefixes = await ResolvePrefixesAsync(databaseName, job.DomainId, CancellationToken.None);

        foreach (var (ip, ports, _, signals, identity) in alive)
        {
            var os = identity.OsFamilyHint;
            switch (os)
            {
                case "windows": win++; break;
                case "linux": lin++; break;
                default: unk++; break;
            }

            DiscoveryHost? adMatch = null;
            adByIp?.TryGetValue(ip, out adMatch);

            DiscoveryHost host;
            if (adMatch is not null)
            {
                var sources = new HashSet<string>(adMatch.Sources ?? [], StringComparer.OrdinalIgnoreCase)
                {
                    "scan",
                    "ad"
                };
                var ips = new HashSet<string>(adMatch.IpAddresses ?? [], StringComparer.OrdinalIgnoreCase) { ip };
                // Prefer AD OS string in OperatingSystem; keep scan family in OsFamilyHint.
                var osFamily = !string.IsNullOrWhiteSpace(adMatch.OperatingSystem)
                    && (adMatch.OperatingSystem.Contains("Windows", StringComparison.OrdinalIgnoreCase)
                        || adMatch.OperatingSystem.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                    ? (adMatch.OperatingSystem.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "windows" : "linux")
                    : os;
                host = new DiscoveryHost
                {
                    DomainId = job.DomainId,
                    ObjectGuid = adMatch.ObjectGuid,
                    SamAccountName = adMatch.SamAccountName,
                    DnsHostName = adMatch.DnsHostName,
                    DisplayName = adMatch.DisplayName ?? adMatch.DnsHostName ?? ip,
                    IpAddresses = ips.ToList(),
                    OperatingSystem = adMatch.OperatingSystem,
                    OperatingSystemVersion = adMatch.OperatingSystemVersion,
                    DistinguishedName = adMatch.DistinguishedName,
                    Sources = sources.ToList(),
                    LastSeenFromAd = adMatch.LastSeenFromAd,
                    LastSeenFromScan = now,
                    OsFamilyHint = osFamily,
                    OpenPorts = ports.ToList(),
                    DeviceRoleHint = identity.DeviceRoleHint,
                    IdentityConfidence = identity.IdentityConfidence,
                    IdentitySummary = identity.IdentitySummary,
                    HttpTitle = signals.HttpTitle,
                    TlsCommonName = signals.TlsCommonName,
                    SshBanner = signals.SshBanner,
                    ScanRunId = runId,
                    AdEnabled = adMatch.AdEnabled,
                    CreatedAt = adMatch.CreatedAt == default ? now : adMatch.CreatedAt,
                    UpdatedAt = now,
                    LastSyncRunId = adMatch.LastSyncRunId
                };
            }
            else
            {
                host = new DiscoveryHost
                {
                    DomainId = job.DomainId,
                    ObjectGuid = $"scan:{ip}",
                    SamAccountName = ip,
                    DisplayName = ip,
                    DnsHostName = null,
                    IpAddresses = [ip],
                    Sources = ["scan"],
                    LastSeenFromScan = now,
                    OsFamilyHint = os,
                    OpenPorts = ports.ToList(),
                    DeviceRoleHint = identity.DeviceRoleHint,
                    IdentityConfidence = identity.IdentityConfidence,
                    IdentitySummary = identity.IdentitySummary,
                    HttpTitle = signals.HttpTitle,
                    TlsCommonName = signals.TlsCommonName,
                    SshBanner = signals.SshBanner,
                    ScanRunId = runId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }

            PrefixTableMatcher.ApplyToHost(host, prefixes);
            upserts.Add(host);
        }

        if (upserts.Count > 0)
            await _hosts.UpsertScanHostsAsync(databaseName, upserts, CancellationToken.None);

        job.Probed = probed;
        job.FoundAlive = alive.Count;
        job.FoundWindows = win;
        job.FoundLinux = lin;
        job.FoundUnknown = unk;
        job.Upserted = upserts.Count;
        job.ProgressPercent = 100;
        job.Status = "completed";
        job.CompletedAt = DateTime.UtcNow;
        await _jobs.UpdateAsync(databaseName, job, CancellationToken.None);

        _logger.LogInformation(
            "Discovery scan completed run={RunId} domain={Domain} alive={Alive} upserted={Upserted}",
            runId, job.DomainId, alive.Count, upserts.Count);
    }

    private async Task<IReadOnlyList<DiscoveryPrefixEntry>> ResolvePrefixesAsync(
        string databaseName,
        string domainId,
        CancellationToken ct)
    {
        try
        {
            var doc = await _prefixes.GetAsync(databaseName, domainId, ct);
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
                    return fromMongo;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load discovery prefixes for {Domain}; using config", domainId);
        }

        return _settings.Value.Discovery?.Prefixes ?? [];
    }

    private async Task<Dictionary<string, DiscoveryHost>> BuildAdIpIndexAsync(
        string databaseName,
        string domainId,
        CancellationToken ct)
    {
        var all = await _hosts.ListAllAsync(databaseName, domainId, ct);
        var map = new Dictionary<string, DiscoveryHost>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in all)
        {
            if (host.Sources?.Any(s => string.Equals(s, "ad", StringComparison.OrdinalIgnoreCase)) != true
                && string.IsNullOrWhiteSpace(host.DistinguishedName))
            {
                // Prefer AD-origin hosts; still allow any host that already has IPs.
            }

            foreach (var ip in host.IpAddresses ?? [])
            {
                if (!string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out _))
                    map[ip.Trim()] = host;
            }

            var dnsName = host.DnsHostName ?? host.DisplayName;
            if (string.IsNullOrWhiteSpace(dnsName) || dnsName.Contains(':'))
                continue;

            // Skip pure IP display names for DNS
            if (IPAddress.TryParse(dnsName, out _))
                continue;

            try
            {
                var addrs = await Dns.GetHostAddressesAsync(dnsName, ct);
                foreach (var a in addrs.Where(x => x.AddressFamily == AddressFamily.InterNetwork))
                {
                    var ip = a.ToString();
                    map.TryAdd(ip, host);
                }
            }
            catch
            {
                // DNS miss — ignore
            }
        }

        return map;
    }
}
