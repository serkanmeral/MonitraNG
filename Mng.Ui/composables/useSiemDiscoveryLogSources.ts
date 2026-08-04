import { computed, onMounted, onUnmounted, ref } from 'vue';
import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type {
  SiemLogSource,
  SiemLogSourceCoverage,
  SiemLogSourceDetailSummary,
  SiemLogSourceKpi,
  SiemLogSourceSeed,
} from '@/types/apps/siemLogSource';
import { looksLikeIpv4 } from '@/utils/secEventHostLabels';

/** Recent syslog → logOnline. */
export const LOG_SOURCE_ONLINE_MS = 15 * 60 * 1000;

/** How far back we look for FortiGate (and seed silence). */
export const LOG_SOURCE_LOOKBACK_HOURS = 24;

export const SIEM_LOG_SOURCE_SEEDS: SiemLogSourceSeed[] = [
  {
    id: 'seed-fortigate',
    kind: 'firewall',
    vendor: 'fortigate',
    product: 'fortigate',
    displayName: 'FortiGate',
    siteLabel: 'Odak',
  },
];

function coverageColor(status: SiemLogSourceCoverage): string {
  switch (status) {
    case 'logOnline':
      return 'success';
    case 'logSilent':
      return 'warning';
    case 'configuredMissing':
      return 'error';
    default:
      return 'secondary';
  }
}

function resolveCoverage(lastEventAt: string | null | undefined, hasEvents: boolean): SiemLogSourceCoverage {
  if (!hasEvents || !lastEventAt) return 'configuredMissing';
  const ts = Date.parse(lastEventAt);
  if (!Number.isFinite(ts)) return 'logSilent';
  if (Date.now() - ts <= LOG_SOURCE_ONLINE_MS) return 'logOnline';
  return 'logSilent';
}

function groupFortiEvents(items: SecEventListItem[]): Map<string, {
  sensorHost: string;
  lastEventAt: string;
  lastAction: string;
  count: number;
}> {
  const map = new Map<string, {
    sensorHost: string;
    lastEventAt: string;
    lastAction: string;
    count: number;
  }>();

  for (const item of items) {
    const host = (item.sourceHost || '').trim() || 'unknown';
    const key = host.toLowerCase();
    const existing = map.get(key);
    const ts = item.timestamp || item.ingestedAt || '';
    if (!existing) {
      map.set(key, {
        sensorHost: host,
        lastEventAt: ts,
        lastAction: item.eventAction || '',
        count: 1,
      });
      continue;
    }
    existing.count += 1;
    if (ts && (!existing.lastEventAt || Date.parse(ts) > Date.parse(existing.lastEventAt))) {
      existing.lastEventAt = ts;
      existing.lastAction = item.eventAction || existing.lastAction;
    }
  }
  return map;
}

export function logSourceEventsLink(source: Pick<SiemLogSource, 'product' | 'sensorHost'>): string {
  const q = new URLSearchParams();
  q.set('sourceProduct', source.product || 'fortigate');
  q.set('timeRange', '24h');
  const host = (source.sensorHost || '').trim();
  if (host && host !== '—' && host !== 'unknown') q.set('sourceHost', host);
  return `/apps/siem-center/events?${q.toString()}`;
}

function usableSensorHost(host?: string | null): string | undefined {
  const h = (host ?? '').trim();
  if (!h || h === '—' || h === 'unknown') return undefined;
  return h;
}

/** Thin triage fetch: 1h/24h totals + top actions + recent sample. */
export async function loadSiemLogSourceDetail(
  source: Pick<SiemLogSource, 'product' | 'sensorHost'>,
): Promise<SiemLogSourceDetailSummary> {
  const product = source.product || 'fortigate';
  const sourceHost = usableSensorHost(source.sensorHost);
  const from1h = new Date(Date.now() - 3600 * 1000).toISOString();
  const from24h = new Date(Date.now() - LOG_SOURCE_LOOKBACK_HOURS * 3600 * 1000).toISOString();

  const base = {
    sourceProduct: product,
    sourceHost,
    excludeUnknown: false as const,
  };

  const [res1h, res24h, sample] = await Promise.all([
    secEventQuery({ ...base, from: from1h, skip: 0, limit: 1 }),
    secEventQuery({ ...base, from: from24h, skip: 0, limit: 1 }),
    secEventQuery({ ...base, from: from24h, skip: 0, limit: 100 }),
  ]);

  const actionCounts = new Map<string, number>();
  for (const item of sample.items) {
    const action = (item.eventAction || 'unknown').trim() || 'unknown';
    actionCounts.set(action, (actionCounts.get(action) ?? 0) + 1);
  }
  const topActions = [...actionCounts.entries()]
    .map(([action, count]) => ({ action, count }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 8);

  const recent = sample.items.slice(0, 8).map((item) => ({
    id: item.id,
    timestamp: item.timestamp,
    action: item.eventAction,
    outcome: item.eventOutcome,
    srcIp: item.networkSrcIp,
    dstIp: item.networkDstIp,
  }));

  return {
    eventCount1h: res1h.total,
    eventCount24h: res24h.total,
    topActions,
    recent,
  };
}

export { coverageColor as logSourceCoverageColor };

export function useSiemDiscoveryLogSources() {
  const loading = ref(false);
  const error = ref<string | null>(null);
  const sources = ref<SiemLogSource[]>([]);
  const lastRefreshedAt = ref<number | null>(null);
  let timer: ReturnType<typeof setInterval> | null = null;
  let visible = true;

  const kpis = computed((): SiemLogSourceKpi[] => {
    const list = sources.value;
    const online = list.filter((s) => s.coverage === 'logOnline').length;
    const silent = list.filter((s) => s.coverage === 'logSilent').length;
    const missing = list.filter((s) => s.coverage === 'configuredMissing').length;
    return [
      {
        id: 'all',
        labelKey: 'siemCenter.discovery.logSources.kpiTotal',
        value: list.length,
        color: 'primary',
        coverage: 'all',
      },
      {
        id: 'online',
        labelKey: 'siemCenter.discovery.logSources.kpiOnline',
        value: online,
        color: coverageColor('logOnline'),
        coverage: 'logOnline',
      },
      {
        id: 'silent',
        labelKey: 'siemCenter.discovery.logSources.kpiSilent',
        value: silent,
        color: coverageColor('logSilent'),
        coverage: 'logSilent',
      },
      {
        id: 'missing',
        labelKey: 'siemCenter.discovery.logSources.kpiMissing',
        value: missing,
        color: coverageColor('configuredMissing'),
        coverage: 'configuredMissing',
      },
    ];
  });

  async function refresh() {
    loading.value = true;
    error.value = null;
    try {
      const from = new Date(Date.now() - LOG_SOURCE_LOOKBACK_HOURS * 3600 * 1000).toISOString();
      const res = await secEventQuery({
        from,
        sourceProduct: 'fortigate',
        excludeUnknown: false,
        skip: 0,
        limit: 200,
      });

      const groups = groupFortiEvents(res.items);
      const built: SiemLogSource[] = [];

      for (const g of groups.values()) {
        const coverage = resolveCoverage(g.lastEventAt, true);
        const ip = looksLikeIpv4(g.sensorHost) ? g.sensorHost : null;
        built.push({
          id: `fortigate:${g.sensorHost.toLowerCase()}`,
          kind: 'firewall',
          vendor: 'fortigate',
          product: 'fortigate',
          displayName: 'FortiGate',
          sensorHost: g.sensorHost,
          sensorIp: ip,
          siteLabel: 'Odak',
          coverage,
          lastEventAt: g.lastEventAt || null,
          lastAction: g.lastAction || null,
          eventCount24h: groups.size === 1
            ? Math.max(g.count, res.total)
            : g.count,
          fromSeed: false,
        });
      }

      // Ensure each seed vendor appears even when silent / missing.
      for (const seed of SIEM_LOG_SOURCE_SEEDS) {
        const hasVendor = built.some((s) => s.vendor === seed.vendor);
        if (hasVendor) continue;
        const host = (seed.sensorHost || '').trim() || '—';
        built.push({
          id: seed.id,
          kind: seed.kind,
          vendor: seed.vendor,
          product: seed.product,
          displayName: seed.displayName,
          sensorHost: host,
          sensorIp: looksLikeIpv4(host) ? host : null,
          siteLabel: seed.siteLabel ?? null,
          coverage: 'configuredMissing',
          lastEventAt: null,
          lastAction: null,
          eventCount24h: 0,
          fromSeed: true,
        });
      }

      built.sort((a, b) => {
        const rank = (c: SiemLogSourceCoverage) =>
          (c === 'logOnline' ? 0 : c === 'logSilent' ? 1 : 2);
        const d = rank(a.coverage) - rank(b.coverage);
        if (d !== 0) return d;
        return a.displayName.localeCompare(b.displayName);
      });

      sources.value = built;
      lastRefreshedAt.value = Date.now();
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e);
      // Keep last good data if any
      if (!sources.value.length) {
        sources.value = SIEM_LOG_SOURCE_SEEDS.map((seed) => ({
          id: seed.id,
          kind: seed.kind,
          vendor: seed.vendor,
          product: seed.product,
          displayName: seed.displayName,
          sensorHost: seed.sensorHost?.trim() || '—',
          sensorIp: null,
          siteLabel: seed.siteLabel ?? null,
          coverage: 'configuredMissing' as const,
          lastEventAt: null,
          lastAction: null,
          eventCount24h: 0,
          fromSeed: true,
        }));
      }
    } finally {
      loading.value = false;
    }
  }

  function startPolling(intervalMs = 30_000) {
    stopPolling();
    timer = setInterval(() => {
      if (!visible || document.hidden) return;
      void refresh();
    }, intervalMs);
  }

  function stopPolling() {
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
  }

  function onVisibility() {
    visible = !document.hidden;
    if (visible) void refresh();
  }

  onMounted(() => {
    void refresh();
    startPolling();
    if (import.meta.client) {
      document.addEventListener('visibilitychange', onVisibility);
    }
  });

  onUnmounted(() => {
    stopPolling();
    if (import.meta.client) {
      document.removeEventListener('visibilitychange', onVisibility);
    }
  });

  return {
    loading,
    error,
    sources,
    kpis,
    lastRefreshedAt,
    refresh,
    coverageColor,
  };
}
