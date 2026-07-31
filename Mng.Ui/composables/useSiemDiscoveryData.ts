import { computed, onMounted, onUnmounted, ref, toValue, type MaybeRefOrGetter } from 'vue';
import { secEventQuery } from '@/services/secEventService';
import {
  fetchDiscoveryHosts,
  triggerDiscoverySync,
  type DiscoveryHostDto,
} from '@/services/siemDiscoveryService';
import {
  buildLegend,
  buildMockKpis,
} from '@/composables/useSiemDiscoveryMock';
import type {
  SiemCoverageStatus,
  SiemDiscoveryAgentInfo,
  SiemDiscoveryBranch,
  SiemDiscoveryFacet,
  SiemDiscoveryHost,
  SiemDiscoveryHostSession,
} from '@/types/apps/siemDiscovery';

/** host.up older than this → Managed Offline (aligned with ~20s agent heartbeat). */
export const DISCOVERY_STALE_MS = 2 * 60 * 1000;

/** Coverage poll interval while the Discovery page is visible. */
export const DISCOVERY_COVERAGE_POLL_MS = 30 * 1000;

interface LiveHostSnapshot {
  lastSeenAt: number;
  agent: SiemDiscoveryAgentInfo | null;
}

export function hostEventsLink(host: Pick<SiemDiscoveryHost, 'hostname' | 'ip'>): string {
  const q = new URLSearchParams();
  q.set('sourceType', 'metric');
  q.set('eventAction', 'host.up');
  q.set('timeRange', '24h');
  const term = (host.hostname || host.ip || '').trim();
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
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

function shortHostKey(hostname: string): string {
  const h = hostname.trim().toLowerCase();
  const bare = h.split('.')[0] || h;
  return bare;
}

function subnetLabel(ip: string): string {
  const parts = ip.split('.');
  if (parts.length === 4 && parts.every((p) => /^\d+$/.test(p))) {
    return `${parts[0]}.${parts[1]}.${parts[2]}.0/24`;
  }
  return 'No IP (AD)';
}

function osFamily(osHint?: string | null): string {
  const s = (osHint || '').toLowerCase();
  if (!s) return 'Unknown OS';
  if (s.includes('server')) return 'Windows Server';
  if (s.includes('windows')) return 'Windows Client';
  if (s.includes('linux') || s.includes('ubuntu') || s.includes('centos') || s.includes('redhat')) {
    return 'Linux';
  }
  return osHint!.trim() || 'Unknown OS';
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

export function parseHostUpAgent(fields?: Record<string, unknown> | null): SiemDiscoveryAgentInfo | null {
  if (!fields || typeof fields !== 'object') return null;
  const primaryIp = asString(fields.primaryIp);
  const ipAddresses = asStringList(fields.ipAddresses);
  const consoleUser = asString(fields.consoleUser);
  const loggedOnUsers = asStringList(fields.loggedOnUsers);
  const bootTimeUtc = asString(fields.bootTimeUtc);
  const uptimeSeconds = asNumber(fields.uptimeSeconds);
  const agentVersion = asString(fields.agentVersion);
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
): SiemDiscoveryHost {
  return {
    id: dto.id || `ad-${dto.samAccountName}`,
    hostname: dto.hostname || dto.samAccountName.replace(/\$$/, ''),
    ip: resolveDisplayIp(dto.ip, agent),
    osHint: dto.osHint || undefined,
    coverage,
    samAccountName: dto.samAccountName || undefined,
    sources: dto.sources?.length ? [...dto.sources] : undefined,
    lastSeenFromAd: dto.lastSeenFromAd ?? null,
    lastSeenAt,
    agent,
  };
}

function groupHosts(
  facet: SiemDiscoveryFacet,
  hosts: SiemDiscoveryHost[],
): SiemDiscoveryBranch[] {
  const buckets = new Map<string, SiemDiscoveryBranch>();

  for (const host of hosts) {
    let id: string;
    let label: string;
    let detail: string | undefined;

    if (facet === 'subnet' || facet === 'vlan') {
      const net = subnetLabel(host.ip === '—' ? '' : host.ip);
      id = `net-${net}`;
      label = facet === 'vlan' ? net : net;
      detail = facet === 'vlan' ? 'AD / IP heuristic' : undefined;
    } else if (facet === 'dhcp') {
      id = 'src-ad';
      label = 'Active Directory';
      detail = 'DHCP not wired yet';
    } else {
      // ap — group by OS until AP/DHCP exist
      const fam = osFamily(host.osHint);
      id = `os-${fam}`;
      label = fam;
      detail = 'OS from AD';
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
      usingLiveDiscovery.value = true;
      lastRefreshedAt.value = Date.now();
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e);
      discovered.value = [];
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

    const mapped: SiemDiscoveryHost[] = discovered.value.map((dto) => {
      const keys = [
        dto.hostname.trim().toLowerCase(),
        shortHostKey(dto.hostname),
        dto.samAccountName.replace(/\$$/, '').toLowerCase(),
      ];
      let snap: LiveHostSnapshot | null = null;
      for (const k of keys) {
        const cur = live.get(k);
        if (cur != null && (snap == null || cur.lastSeenAt > snap.lastSeenAt)) snap = cur;
      }
      const lastSeen = snap?.lastSeenAt ?? null;
      const fromLive = coverageFromLastSeen(lastSeen);
      return toViewHost(dto, fromLive ?? 'discoveredUnmanaged', lastSeen, snap?.agent ?? null);
    });

    const base = groupHosts(facetValue, mapped);

    const known = new Set(
      mapped.flatMap((h) => [h.hostname.trim().toLowerCase(), shortHostKey(h.hostname)]),
    );
    const extras: SiemDiscoveryHost[] = [];
    for (const [hostKey, snap] of live) {
      if (hostKey.includes('.')) continue; // prefer short keys for extras
      if (known.has(hostKey)) continue;
      const cov = coverageFromLastSeen(snap.lastSeenAt);
      if (!cov) continue;
      extras.push({
        id: `live-${hostKey}`,
        hostname: hostKey,
        ip: resolveDisplayIp(null, snap.agent),
        osHint: 'agent',
        coverage: cov,
        sources: ['agent'],
        lastSeenAt: snap.lastSeenAt,
        agent: snap.agent,
      });
    }

    if (extras.length) {
      base.push({
        id: '__live-unplaced',
        label: 'Live agents',
        detail: 'host.up',
        hosts: extras,
      });
    }

    return base;
  });

  const allHosts = computed(() => branches.value.flatMap((b) => b.hosts));
  const legend = computed(() => buildLegend(allHosts.value));
  const kpis = computed(() => buildMockKpis());

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
