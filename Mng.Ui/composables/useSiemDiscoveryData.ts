import { computed, onMounted, ref, toValue, type MaybeRefOrGetter } from 'vue';
import { secEventQuery } from '@/services/secEventService';
import {
  DISCOVERY_FACETS,
  buildLegend,
  buildMockKpis,
  coverageColor,
  getMockBranches,
  getMockHosts,
} from '@/composables/useSiemDiscoveryMock';
import type {
  SiemCoverageStatus,
  SiemDiscoveryBranch,
  SiemDiscoveryFacet,
  SiemDiscoveryHost,
} from '@/types/apps/siemDiscovery';

export { DISCOVERY_FACETS, coverageColor } from '@/composables/useSiemDiscoveryMock';

/** host.up older than this → Managed Offline */
export const DISCOVERY_STALE_MS = 15 * 60 * 1000;

export function hostEventsLink(host: Pick<SiemDiscoveryHost, 'hostname' | 'ip'>): string {
  const q = new URLSearchParams();
  q.set('sourceType', 'metric');
  q.set('eventAction', 'host.up');
  q.set('timeRange', '24h');
  const term = (host.hostname || host.ip || '').trim();
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
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

/**
 * Loads live host.up heartbeat map and merges into mock topology branches.
 */
export function useSiemDiscoveryData(facet: MaybeRefOrGetter<SiemDiscoveryFacet>) {
  const loading = ref(false);
  const error = ref<string | null>(null);
  const liveByHost = ref<Map<string, number>>(new Map());
  const lastRefreshedAt = ref<number | null>(null);

  async function refresh() {
    loading.value = true;
    error.value = null;
    try {
      const res = await secEventQuery({
        from: from24h(),
        sourceType: 'metric',
        eventAction: 'host.up',
        limit: 100,
        excludeUnknown: false,
      });
      const map = new Map<string, number>();
      for (const item of res.items ?? []) {
        const key = (item.sourceHost || '').trim().toLowerCase();
        if (!key) continue;
        const ts = Date.parse(item.timestamp || item.ingestedAt || '');
        if (!Number.isFinite(ts)) continue;
        const prev = map.get(key);
        if (prev == null || ts > prev) map.set(key, ts);
      }
      liveByHost.value = map;
      lastRefreshedAt.value = Date.now();
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : String(e);
      liveByHost.value = new Map();
    } finally {
      loading.value = false;
    }
  }

  onMounted(() => {
    void refresh();
  });

  const branches = computed((): SiemDiscoveryBranch[] => {
    const facetValue = toValue(facet);
    const base = getMockBranches(facetValue).map((b) => ({
      ...b,
      hosts: b.hosts.map((h) => {
        const key = h.hostname.trim().toLowerCase();
        const live = coverageFromLastSeen(liveByHost.value.get(key) ?? null);
        return live ? { ...h, coverage: live } : { ...h };
      }),
    }));

    const known = new Set(getMockHosts().map((h) => h.hostname.trim().toLowerCase()));
    const extras: SiemDiscoveryHost[] = [];
    for (const [hostKey, ts] of liveByHost.value) {
      if (known.has(hostKey)) continue;
      const cov = coverageFromLastSeen(ts);
      if (!cov) continue;
      extras.push({
        id: `live-${hostKey}`,
        hostname: hostKey,
        ip: '—',
        osHint: 'agent',
        coverage: cov,
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
    error,
    lastRefreshedAt,
    branches,
    legend,
    kpis,
    refresh,
  };
}
