import { computed, onMounted, onUnmounted, ref, toValue, type MaybeRefOrGetter } from 'vue';
import { secEventQuery } from '@/services/secEventService';
import {
  fetchDiscoveryHosts,
  triggerDiscoverySync,
  type DiscoveryHostDto,
  type DiscoveryPrefixDto,
} from '@/services/siemDiscoveryService';
import {
  buildCoverageKpis,
  buildLegend,
} from '@/composables/useSiemDiscoveryMock';
import { hostMetricsEventsLink } from '@/composables/useSiemDiscoveryHostMetrics';
import { hostWatchEventsLink } from '@/composables/useSiemDiscoveryHostApps';
import { hostEventLogEventsLink } from '@/composables/useSiemDiscoveryHostEventLogs';
import type {
  SiemCoverageStatus,
  SiemDiscoveryAgentInfo,
  SiemDiscoveryBranch,
  SiemDiscoveryFacet,
  SiemDiscoveryHost,
  SiemDiscoveryHostSession,
} from '@/types/apps/siemDiscovery';
import { resolveOsFamily } from '@/utils/siemDiscoveryOs';
import {
  isIpv4Literal,
  resolveDisplayHostname,
  secEventMatchesDiscoveryHost,
  shortHostKey,
} from '@/utils/siemDiscoveryHostMatch';
import {
  DEFAULT_DISCOVERY_PREFIXES,
  NO_IP_SITE,
  UNSCOPED_SITE,
  resolveBestSiteBucket,
  type DiscoveryPrefix,
} from '@/utils/discoveryPrefixTable';

/** host.up older than this → Managed Offline (aligned with ~20s agent heartbeat). */
export const DISCOVERY_STALE_MS = 2 * 60 * 1000;

/** Coverage poll interval while the Discovery page is visible. */
export const DISCOVERY_COVERAGE_POLL_MS = 30 * 1000;

interface LiveHostSnapshot {
  lastSeenAt: number;
  agent: SiemDiscoveryAgentInfo | null;
}

/** Deep-link: host heartbeat / status events for a discovery host. */
export function hostEventsLink(host: Pick<SiemDiscoveryHost, 'hostname' | 'ip'>): string {
  const q = new URLSearchParams();
  q.set('sourceType', 'metric');
  q.set('eventAction', 'host.up');
  q.set('timeRange', '24h');
  const hostname = (host.hostname || '').trim();
  const term = hostname ? shortHostKey(hostname) : (host.ip || '').trim();
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
}

export type HostDashboardTab = 'status' | 'metrics' | 'apps' | 'eventlog';

const HOST_DASHBOARD_TABS = new Set<string>(['status', 'metrics', 'apps', 'eventlog']);

export function parseHostDashboardTab(raw: unknown): HostDashboardTab {
  const t = typeof raw === 'string' ? raw.trim().toLowerCase() : '';
  return HOST_DASHBOARD_TABS.has(t) ? (t as HostDashboardTab) : 'status';
}

/** Full-page host dashboard route (short hostname key). */
export function hostDashboardLink(
  hostname: string,
  tab?: HostDashboardTab | string | null,
): string {
  const key = shortHostKey(hostname) || hostname.trim().toLowerCase();
  const q = new URLSearchParams();
  if (tab != null && String(tab).trim()) {
    q.set('tab', parseHostDashboardTab(tab));
  }
  const qs = q.toString();
  return `/apps/siem-center/hosts/${encodeURIComponent(key)}${qs ? `?${qs}` : ''}`;
}

/** Security-events deep-link shaped by dashboard / modal tab. */
export function hostTabEventsLink(
  host: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'osFamily' | 'agent'> | string,
  tab: HostDashboardTab | string,
): string {
  const hostname = typeof host === 'string' ? host : (host.hostname || host.ip || '');
  const osFamily = typeof host === 'string' ? undefined : host.osFamily;
  const hints = typeof host === 'string'
    ? { hostname, ip: hostname, agent: null }
    : host;
  const t = parseHostDashboardTab(tab);
  switch (t) {
    case 'metrics':
      return hostMetricsEventsLink(hostname, hints);
    case 'apps':
      return hostWatchEventsLink(hostname, hints);
    case 'eventlog':
      return hostEventLogEventsLink(hostname, osFamily, hints);
    case 'status':
    default:
      return typeof host === 'string'
        ? hostEventsLink({ hostname: host, ip: '' })
        : hostEventsLink(host);
  }
}

/**
 * Resolve a host view-model for the host dashboard page (AD inventory + latest host.up).
 * Route may be a scan IP (e.g. 192.168.20.20) while host.up is indexed as machine name.
 */
export async function loadHostDashboardHost(routeHostname: string): Promise<SiemDiscoveryHost> {
  const want = shortHostKey(routeHostname) || routeHostname.trim().toLowerCase();
  const displayName = routeHostname.trim() || want;
  const routeIsIp = isIpv4Literal(want);

  let dto: DiscoveryHostDto | null = null;
  let prefixes: DiscoveryPrefix[] = DEFAULT_DISCOVERY_PREFIXES;
  try {
    const hostsRes = await fetchDiscoveryHosts({ limit: 2000 });
    prefixes = toPrefixes(hostsRes.prefixes);
    for (const h of hostsRes.items ?? []) {
      const hn = (h.hostname || h.samAccountName?.replace(/\$$/, '') || '').trim();
      const ip = (h.ip || '').trim().toLowerCase();
      if (!hn && !ip) continue;
      const hnKey = shortHostKey(hn);
      if (
        (hn && (hnKey === want || hn.toLowerCase() === want))
        || (ip && ip === want)
      ) {
        dto = h;
        break;
      }
    }
  } catch {
    dto = null;
  }

  const matchHints: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> = {
    hostname: dto?.hostname || displayName,
    ip: dto?.ip || (routeIsIp ? want : '—'),
    agent: null,
  };

  let lastSeenAt: number | null = null;
  let agent: SiemDiscoveryAgentInfo | null = null;
  try {
    // Prefer named search when we already know the inventory hostname; for bare IP
    // pull a wider host.up window and match on primaryIp / machine client-side.
    const searchTerm = routeIsIp
      ? undefined
      : (shortHostKey(dto?.hostname || want) || want);
    const res = await secEventQuery({
      from: from24h(),
      sourceType: 'metric',
      eventAction: 'host.up',
      ...(searchTerm ? { search: searchTerm } : {}),
      limit: routeIsIp ? 500 : 80,
      excludeUnknown: false,
    });
    for (const item of res.items ?? []) {
      if (!secEventMatchesDiscoveryHost(item, want, matchHints)) continue;
      const ts = Date.parse(item.timestamp || item.ingestedAt || '');
      if (!Number.isFinite(ts)) continue;
      if (lastSeenAt != null && ts <= lastSeenAt) continue;
      lastSeenAt = ts;
      agent = parseHostUpAgent(item.fields ?? null);
    }
  } catch {
    // keep defaults
  }

  const liveCoverage = coverageFromLastSeen(lastSeenAt);
  if (dto) {
    const baseCoverage: SiemCoverageStatus =
      liveCoverage ?? 'managedOffline';
    return toViewHost(dto, baseCoverage, lastSeenAt, agent, prefixes);
  }

  const liveOnly: SiemDiscoveryHost = {
    id: `live-${want}`,
    hostname: resolveDisplayHostname(displayName || want, agent),
    ip: agent?.primaryIp || (routeIsIp ? want : '—'),
    osHint: agent?.platform || undefined,
    coverage: liveCoverage ?? 'discoveredUnmanaged',
    lastSeenAt,
    agent,
  };
  liveOnly.osFamily = resolveOsFamily(liveOnly);
  return liveOnly;
}

/** Default MngLogs Local UI port (agent system.json LocalUiPort). */
export const DISCOVERY_LOCAL_UI_PORT = 5092;

/**
 * Agent Local UI URL for a discovery host (new browser tab).
 * Uses host.up localUiPort when present; otherwise default 5092.
 * Requires a reachable IP and agent LocalUiHost beyond loopback for remote open.
 */
export function hostLocalUiLink(
  host: Pick<SiemDiscoveryHost, 'ip' | 'agent'>,
  fallbackPort: number = DISCOVERY_LOCAL_UI_PORT,
): string | null {
  const ip = (host.agent?.primaryIp || host.ip || '').trim();
  if (!ip || ip === '—') return null;
  const fromAgent = host.agent?.localUiPort;
  const port =
    typeof fromAgent === 'number' && fromAgent > 0 && fromAgent <= 65535
      ? Math.floor(fromAgent)
      : fallbackPort > 0
        ? fallbackPort
        : DISCOVERY_LOCAL_UI_PORT;
  return `http://${ip}:${port}/`;
}

function from24h(): string {
  return new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString();
}

function coverageFromLastSeen(lastSeenMs: number | null): SiemCoverageStatus | null {
  if (lastSeenMs == null) return null;
  const age = Date.now() - lastSeenMs;
  if (age <= DISCOVERY_STALE_MS) return 'managedOnline';
  return 'managedOffline';
}

function toPrefixes(raw?: DiscoveryPrefixDto[] | null): DiscoveryPrefix[] {
  if (!raw?.length) return DEFAULT_DISCOVERY_PREFIXES;
  const mapped = raw
    .filter((p) => !!p?.cidr)
    .map((p) => ({
      cidr: p.cidr,
      label: p.label || p.cidr,
      vlanName: p.vlanName ?? null,
    }));
  return mapped.length ? mapped : DEFAULT_DISCOVERY_PREFIXES;
}

function asString(v: unknown): string | null {
  if (v == null) return null;
  if (typeof v === 'string') {
    const t = v.trim();
    return t || null;
  }
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  return null;
}

function asNumber(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string' && v.trim()) {
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

function asStringList(v: unknown): string[] {
  if (!Array.isArray(v)) return [];
  return v.map(asString).filter((x): x is string => !!x);
}

function parseSessions(v: unknown): SiemDiscoveryHostSession[] {
  if (!Array.isArray(v)) return [];
  const out: SiemDiscoveryHostSession[] = [];
  for (const row of v) {
    if (!row || typeof row !== 'object') continue;
    const r = row as Record<string, unknown>;
    const user = asString(r.user);
    if (!user) continue;
    out.push({
      user,
      sessionId: asNumber(r.sessionId) ?? undefined,
      state: asString(r.state) ?? undefined,
      stationName: asString(r.stationName),
      clientProtocol: asString(r.clientProtocol),
      logonAtUtc: asString(r.logonAtUtc),
      durationSeconds: asNumber(r.durationSeconds),
    });
  }
  return out;
}

function inferPlatformFromOs(os: string | null | undefined): string | null {
  const s = (os || '').trim().toLowerCase();
  if (!s) return null;
  if (s.includes('windows') || s.includes('win32') || s.includes('win64')) return 'windows';
  if (
    s.includes('linux')
    || s.includes('ubuntu')
    || s.includes('debian')
    || s.includes('centos')
    || s.includes('redhat')
    || s.includes('rhel')
  ) {
    return 'linux';
  }
  return null;
}

export function parseHostUpAgent(fields?: Record<string, unknown> | null): SiemDiscoveryAgentInfo | null {
  if (!fields || typeof fields !== 'object') return null;
  const primaryIp = asString(fields.primaryIp);
  const ipAddresses = asStringList(fields.ipAddresses);
  const consoleUser = asString(fields.consoleUser);
  const loggedOnUsers = asStringList(fields.loggedOnUsers);
  const bootTimeUtc = asString(fields.bootTimeUtc);
  const uptimeSeconds = asNumber(fields.uptimeSeconds);
  const agentVersion = asString(fields.agentVersion);
  const os = asString(fields.os);
  const platform = asString(fields.platform) || inferPlatformFromOs(os);
  const machine = asString(fields.machine);
  const localUiPort = asNumber(fields.localUiPort);
  const localUiHost = asString(fields.localUiHost);
  const localUiRemoteAccess =
    typeof fields.localUiRemoteAccess === 'boolean'
      ? fields.localUiRemoteAccess
      : null;
  const sessions = parseSessions(fields.loggedOnSessions);
  if (
    !primaryIp
    && !ipAddresses.length
    && !consoleUser
    && !loggedOnUsers.length
    && !bootTimeUtc
    && uptimeSeconds == null
    && !agentVersion
    && !platform
    && !machine
    && localUiPort == null
    && !localUiHost
    && !sessions.length
  ) {
    return null;
  }
  return {
    primaryIp,
    ipAddresses: ipAddresses.length ? ipAddresses : undefined,
    consoleUser,
    loggedOnUsers: loggedOnUsers.length ? loggedOnUsers : undefined,
    bootTimeUtc,
    uptimeSeconds,
    agentVersion,
    platform,
    machine,
    localUiPort,
    localUiHost,
    localUiRemoteAccess,
    sessions: sessions.length ? sessions : undefined,
  };
}

function resolveDisplayIp(adIp: string | null | undefined, agent: SiemDiscoveryAgentInfo | null): string {
  const fromAgent = agent?.primaryIp?.trim();
  if (fromAgent) return fromAgent;
  const fromAd = (adIp || '').trim();
  if (fromAd && fromAd !== '—') return fromAd;
  return '—';
}

function toViewHost(
  dto: DiscoveryHostDto,
  coverage: SiemCoverageStatus,
  lastSeenAt: number | null,
  agent: SiemDiscoveryAgentInfo | null,
  prefixes: DiscoveryPrefix[],
): SiemDiscoveryHost {
  const openPorts = dto.openPorts?.length ? [...dto.openPorts] : undefined;
  const ip = resolveDisplayIp(dto.ip, agent);
  // Site from scan/AD IP first — agent primaryIp can sit outside the prefix table.
  const site = resolveBestSiteBucket(
    [dto.ip, ip, agent?.primaryIp, ...(agent?.ipAddresses ?? [])],
    prefixes,
    { siteLabel: dto.siteLabel, subnetCidr: dto.subnetCidr },
  );
  const rawHostname = dto.hostname || dto.samAccountName.replace(/\$$/, '');
  const draft: SiemDiscoveryHost = {
    id: dto.id || `ad-${dto.samAccountName}`,
    // Scan often stores bare IP as hostname; prefer agent MachineName when present.
    hostname: resolveDisplayHostname(rawHostname, agent),
    ip,
    osHint: dto.osHint || undefined,
    openPorts,
    deviceRoleHint: dto.deviceRoleHint || undefined,
    identityConfidence: dto.identityConfidence || undefined,
    identitySummary: dto.identitySummary || undefined,
    httpTitle: dto.httpTitle || undefined,
    tlsCommonName: dto.tlsCommonName || undefined,
    sshBanner: dto.sshBanner || undefined,
    subnetCidr: site.subnetCidr || dto.subnetCidr || undefined,
    siteLabel: site.label !== NO_IP_SITE ? site.label : (dto.siteLabel || undefined),
    vlanName: dto.vlanName || undefined,
    coverage,
    samAccountName: dto.samAccountName || undefined,
    sources: dto.sources?.length ? [...dto.sources] : undefined,
    lastSeenFromAd: dto.lastSeenFromAd ?? null,
    lastSeenAt,
    agent,
  };
  draft.osFamily = resolveOsFamily(draft);
  return draft;
}

function groupHosts(
  facet: SiemDiscoveryFacet,
  hosts: SiemDiscoveryHost[],
  prefixes: DiscoveryPrefix[],
): SiemDiscoveryBranch[] {
  const buckets = new Map<string, SiemDiscoveryBranch>();

  for (const host of hosts) {
    let id: string;
    let label: string;
    let detail: string | undefined;

    if (facet === 'subnet') {
      // Prefer already-resolved site on the host (scan IP / API enrich), not only display IP.
      if (host.siteLabel && host.siteLabel !== UNSCOPED_SITE && host.siteLabel !== NO_IP_SITE) {
        id = host.subnetCidr ? `site-${host.subnetCidr}` : `site-label-${host.siteLabel}`;
        label = host.siteLabel;
        detail = host.subnetCidr;
      } else {
        const site = resolveBestSiteBucket(
          [host.ip, host.agent?.primaryIp, ...(host.agent?.ipAddresses ?? [])],
          prefixes,
          { siteLabel: host.siteLabel, subnetCidr: host.subnetCidr },
        );
        id = site.id;
        label = site.label;
        detail = site.subnetCidr || site.detail;
      }
    } else if (facet === 'vlan') {
      // Only real operator-mapped VLAN; never invent from IP.
      const vlan = (host.vlanName || '').trim();
      if (vlan) {
        id = `vlan-${vlan}`;
        label = vlan;
        detail = host.subnetCidr;
      } else {
        id = 'vlan-unknown';
        label = 'Unknown VLAN';
        detail = 'No VLAN mapping on prefix table';
      }
    } else if (facet === 'dhcp') {
      id = 'src-ad';
      label = 'Active Directory';
      detail = 'DHCP not wired yet';
    } else {
      // ap — group by OS family until AP/DHCP exist
      const fam = resolveOsFamily(host);
      id = `os-${fam}`;
      label = fam === 'windows' ? 'Windows' : fam === 'linux' ? 'Linux' : 'Unknown OS';
      detail = host.osHint && host.osHint !== fam ? host.osHint : undefined;
    }

    let branch = buckets.get(id);
    if (!branch) {
      branch = { id, label, detail, hosts: [] };
      buckets.set(id, branch);
    }
    branch.hosts.push(host);
  }

  return [...buckets.values()].sort((a, b) => a.label.localeCompare(b.label));
}

function putLive(
  map: Map<string, LiveHostSnapshot>,
  key: string,
  ts: number,
  agent: SiemDiscoveryAgentInfo | null,
) {
  if (!key) return;
  const prev = map.get(key);
  if (prev == null || ts > prev.lastSeenAt) {
    map.set(key, { lastSeenAt: ts, agent });
  }
}

/**
 * Loads discovery_hosts from Collector and merges host.up coverage.
 */
export function useSiemDiscoveryData(facet: MaybeRefOrGetter<SiemDiscoveryFacet>) {
  const loading = ref(false);
  const syncing = ref(false);
  const coverageRefreshing = ref(false);
  const error = ref<string | null>(null);
  const liveByHost = ref<Map<string, LiveHostSnapshot>>(new Map());
  const discovered = ref<DiscoveryHostDto[]>([]);
  const prefixes = ref<DiscoveryPrefix[]>(DEFAULT_DISCOVERY_PREFIXES);
  const lastRefreshedAt = ref<number | null>(null);
  const usingLiveDiscovery = ref(false);
  const autoRefresh = ref(true);

  async function refreshCoverageOnly() {
    const res = await secEventQuery({
      from: from24h(),
      sourceType: 'metric',
      eventAction: 'host.up',
      limit: 500,
      excludeUnknown: false,
    });
    const map = new Map<string, LiveHostSnapshot>();
    for (const item of res.items ?? []) {
      const key = (item.sourceHost || '').trim().toLowerCase();
      if (!key) continue;
      const ts = Date.parse(item.timestamp || item.ingestedAt || '');
      if (!Number.isFinite(ts)) continue;
      const agent = parseHostUpAgent(item.fields ?? null);
      putLive(map, key, ts, agent);
      putLive(map, shortHostKey(key), ts, agent);
      if (agent?.primaryIp?.trim()) {
        putLive(map, agent.primaryIp.trim().toLowerCase(), ts, agent);
      }
      for (const ip of agent?.ipAddresses ?? []) {
        if (ip?.trim()) putLive(map, ip.trim().toLowerCase(), ts, agent);
      }
    }
    liveByHost.value = map;
  }

  async function refreshCoverageQuiet() {
    if (coverageRefreshing.value || loading.value) return;
    coverageRefreshing.value = true;
    try {
      await refreshCoverageOnly();
      lastRefreshedAt.value = Date.now();
    } catch {
      // Keep previous live map; full refresh surfaces hard errors.
    } finally {
      coverageRefreshing.value = false;
    }
  }

  async function refresh() {
    loading.value = true;
    error.value = null;
    try {
      const [hostsRes] = await Promise.all([
        fetchDiscoveryHosts({ limit: 2000 }).catch((e: unknown) => {
          throw e;
        }),
        refreshCoverageOnly().catch(() => {
          liveByHost.value = new Map();
        }),
      ]);
      discovered.value = hostsRes.items ?? [];
      prefixes.value = toPrefixes(hostsRes.prefixes);
      usingLiveDiscovery.value = true;
      lastRefreshedAt.value = Date.now();
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e);
      discovered.value = [];
      prefixes.value = DEFAULT_DISCOVERY_PREFIXES;
      usingLiveDiscovery.value = false;
      try {
        await refreshCoverageOnly();
        lastRefreshedAt.value = Date.now();
      } catch {
        liveByHost.value = new Map();
      }
    } finally {
      loading.value = false;
    }
  }

  async function syncNow() {
    syncing.value = true;
    error.value = null;
    try {
      const result = await triggerDiscoverySync();
      if (result.status === 'error') {
        error.value = result.error || 'Discovery sync failed';
      }
      await refresh();
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e);
    } finally {
      syncing.value = false;
    }
  }

  function setAutoRefresh(enabled: boolean) {
    autoRefresh.value = enabled;
  }

  function toggleAutoRefresh() {
    autoRefresh.value = !autoRefresh.value;
  }

  onMounted(() => {
    void refresh();
  });

  let pollTimer: ReturnType<typeof setInterval> | undefined;
  function onVisibility() {
    if (!document.hidden && autoRefresh.value) {
      void refreshCoverageQuiet();
    }
  }
  if (import.meta.client) {
    pollTimer = setInterval(() => {
      if (!autoRefresh.value) return;
      if (document.hidden) return;
      void refreshCoverageQuiet();
    }, DISCOVERY_COVERAGE_POLL_MS);
    document.addEventListener('visibilitychange', onVisibility);
  }

  onUnmounted(() => {
    if (pollTimer) clearInterval(pollTimer);
    if (import.meta.client) {
      document.removeEventListener('visibilitychange', onVisibility);
    }
  });

  const branches = computed((): SiemDiscoveryBranch[] => {
    const facetValue = toValue(facet);
    const live = liveByHost.value;
    const prefixTable = prefixes.value;

    const mapped: SiemDiscoveryHost[] = discovered.value.map((dto) => {
      const keys = [
        dto.hostname.trim().toLowerCase(),
        shortHostKey(dto.hostname),
        dto.samAccountName.replace(/\$$/, '').toLowerCase(),
        (dto.ip || '').trim().toLowerCase(),
      ].filter(Boolean);
      let snap: LiveHostSnapshot | null = null;
      for (const k of keys) {
        const cur = live.get(k);
        if (cur != null && (snap == null || cur.lastSeenAt > snap.lastSeenAt)) snap = cur;
      }
      const lastSeen = snap?.lastSeenAt ?? null;
      const fromLive = coverageFromLastSeen(lastSeen);
      return toViewHost(
        dto,
        fromLive ?? 'discoveredUnmanaged',
        lastSeen,
        snap?.agent ?? null,
        prefixTable,
      );
    });

    const known = new Set<string>();
    for (const h of mapped) {
      known.add(h.hostname.trim().toLowerCase());
      known.add(shortHostKey(h.hostname));
      if (h.ip && h.ip !== '—') known.add(h.ip.trim().toLowerCase());
      if (h.agent?.primaryIp) known.add(h.agent.primaryIp.trim().toLowerCase());
      for (const ip of h.agent?.ipAddresses ?? []) {
        if (ip?.trim()) known.add(ip.trim().toLowerCase());
      }
    }

    // Agents not yet in discovery_hosts — same tree as everyone else (no separate branch).
    const extras: SiemDiscoveryHost[] = [];
    for (const [hostKey, snap] of live) {
      if (hostKey.includes('.')) continue; // prefer short keys for extras
      if (known.has(hostKey)) continue;
      const pip = (snap.agent?.primaryIp || '').trim().toLowerCase();
      if (pip && known.has(pip)) continue;
      const agentIps = (snap.agent?.ipAddresses ?? [])
        .map((x) => x.trim().toLowerCase())
        .filter(Boolean);
      if (agentIps.some((ip) => known.has(ip))) continue;
      const cov = coverageFromLastSeen(snap.lastSeenAt);
      if (!cov) continue;
      const ip = resolveDisplayIp(null, snap.agent);
      const site = resolveBestSiteBucket(
        [ip, snap.agent?.primaryIp, ...(snap.agent?.ipAddresses ?? [])],
        prefixTable,
      );
      const extra: SiemDiscoveryHost = {
        id: `live-${hostKey}`,
        hostname: hostKey,
        ip,
        osHint: snap.agent?.platform || undefined,
        subnetCidr: site.subnetCidr,
        siteLabel: site.label === NO_IP_SITE ? undefined : site.label,
        coverage: cov,
        sources: ['agent'],
        lastSeenAt: snap.lastSeenAt,
        agent: snap.agent,
      };
      extra.osFamily = resolveOsFamily(extra);
      extras.push(extra);
    }

    return groupHosts(facetValue, [...mapped, ...extras], prefixTable);
  });

  const allHosts = computed(() => branches.value.flatMap((b) => b.hosts));
  const legend = computed(() => buildLegend(allHosts.value));
  const kpis = computed(() => buildCoverageKpis(allHosts.value));

  return {
    loading,
    syncing,
    coverageRefreshing,
    error,
    lastRefreshedAt,
    usingLiveDiscovery,
    autoRefresh,
    pollIntervalMs: DISCOVERY_COVERAGE_POLL_MS,
    staleMs: DISCOVERY_STALE_MS,
    branches,
    legend,
    kpis,
    refresh,
    syncNow,
    setAutoRefresh,
    toggleAutoRefresh,
  };
}
