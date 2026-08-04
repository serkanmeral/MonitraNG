import type {
  SecEventListItem,
  SecEventQuery,
  SecEventQueryResponse,
  SecEventDashboardSummary,
  SecEventScopeOptions,
} from '@/types/apps/secEvent';
import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

async function authHeaders(): Promise<Record<string, string>> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // Server returns 401 if token is missing or invalid.
  }

  const headers: Record<string, string> = {};
  if (authStore.domainName) {
    headers['X-Domain-Name'] = authStore.domainName;
  }
  const token = getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const q = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue;
    q.set(key, String(value));
  }
  const s = q.toString();
  return s ? `?${s}` : '';
}

function normalizeListItem(raw: Record<string, unknown>): SecEventListItem {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    timestamp: String(raw.timestamp ?? raw.Timestamp ?? ''),
    ingestedAt: String(raw.ingestedAt ?? raw.IngestedAt ?? ''),
    sourceType: (raw.sourceType ?? raw.SourceType) as string | null | undefined,
    sourceProduct: (raw.sourceProduct ?? raw.SourceProduct) as string | null | undefined,
    sourceHost: (raw.sourceHost ?? raw.SourceHost) as string | null | undefined,
    eventAction: String(raw.eventAction ?? raw.EventAction ?? ''),
    eventOutcome: (raw.eventOutcome ?? raw.EventOutcome) as string | null | undefined,
    eventCode: (raw.eventCode ?? raw.EventCode) as string | null | undefined,
    actorUser: (raw.actorUser ?? raw.ActorUser) as string | null | undefined,
    networkSrcIp: (raw.networkSrcIp ?? raw.NetworkSrcIp) as string | null | undefined,
    networkDstIp: (raw.networkDstIp ?? raw.NetworkDstIp) as string | null | undefined,
    parserId: (raw.parserId ?? raw.ParserId) as string | null | undefined,
    rawPreview: (raw.rawPreview ?? raw.RawPreview) as string | null | undefined,
    raw: (raw.raw ?? raw.Raw) as string | null | undefined,
    baselineNewFlowPair: Boolean(raw.baselineNewFlowPair ?? raw.BaselineNewFlowPair),
    fields: normalizeFields(raw.fields ?? raw.Fields),
  };
}

function normalizeFields(raw: unknown): Record<string, unknown> | null | undefined {
  if (raw == null) return undefined;
  if (typeof raw !== 'object' || Array.isArray(raw)) return undefined;
  return raw as Record<string, unknown>;
}

function normalizeQueryResponse(raw: Record<string, unknown>): SecEventQueryResponse {
  const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
  const items = Array.isArray(itemsRaw) ? itemsRaw.map(normalizeListItem) : [];
  const total = Number(raw.total ?? raw.Total ?? items.length);
  return { items, total: Number.isFinite(total) ? total : items.length };
}

export async function secEventQuery(query: SecEventQuery = {}): Promise<SecEventQueryResponse> {
  const qs = buildQuery({
    from: query.from,
    to: query.to,
    sourceType: query.sourceType,
    sourceProduct: query.sourceProduct,
    eventAction: query.eventAction,
    eventActions: query.eventActions,
    eventActionPrefix: query.eventActionPrefix,
    eventOutcome: query.eventOutcome,
    srcIp: query.srcIp,
    dstIp: query.dstIp,
    dstPort: query.dstPort,
    actorUser: query.actorUser,
    sourceHost: query.sourceHost,
    sourceHosts: query.sourceHosts,
    eventCode: query.eventCode,
    eventCodes: query.eventCodes,
    search: query.search,
    fieldFilters: query.fieldFilters,
    excludeUnknown: query.excludeUnknown,
    skip: query.skip ?? 0,
    limit: query.limit ?? 50,
  });
  const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events${qs}`, {
    method: 'GET',
    headers: await authHeaders(),
  });
  return normalizeQueryResponse(raw);
}

export async function secEventScopeOptions(options?: {
  rangeHours?: number;
}): Promise<SecEventScopeOptions> {
  const qs = buildQuery({
    rangeHours: options?.rangeHours ?? 168,
  });
  const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events/scope-options${qs}`, {
    method: 'GET',
    headers: await authHeaders(),
  });
  const types = asStringList(raw.types ?? raw.Types);
  const products = asStringList(raw.products ?? raw.Products);
  const hosts = asStringList(raw.hosts ?? raw.Hosts);
  return {
    types,
    products,
    hosts,
    rangeHours: Number(raw.rangeHours ?? raw.RangeHours ?? 168) || 168,
    source: String(raw.source ?? raw.Source ?? ''),
  };
}

function asStringList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .map((x) => String(x ?? '').trim())
    .filter(Boolean);
}

export async function secEventGet(id: string): Promise<SecEventListItem> {
  const trimmed = (id || '').trim();
  if (!trimmed) {
    throw new Error('Missing sec-event id');
  }

  // Prefer query-string lookup: Windows Event Log ids often contain '/'
  // (e.g. "...LocalSessionManager/Operational:2543:25") which breaks path routes / proxies.
  const qs = buildQuery({ id: trimmed });
  try {
    const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events/by-id${qs}`, {
      method: 'GET',
      headers: await authHeaders(),
    });
    return normalizeListItem(raw);
  } catch (err: unknown) {
    const status = (err as { statusCode?: number; status?: number })?.statusCode
      ?? (err as { statusCode?: number; status?: number })?.status;
    // Older Reactor builds may not have by-id yet — fall back to path get for simple ids.
    if (status === 404 && !trimmed.includes('/')) {
      const raw = await $fetch<Record<string, unknown>>(
        `/api/reactor/v1/sec-events/${encodeURIComponent(trimmed)}`,
        {
          method: 'GET',
          headers: await authHeaders(),
        },
      );
      return normalizeListItem(raw);
    }
    throw err;
  }
}

function normalizeDashboardSummary(raw: Record<string, unknown>): SecEventDashboardSummary {
  const byActionRaw = raw.byAction ?? raw.ByAction;
  const hourlyRaw = raw.hourly ?? raw.Hourly;
  const byAction =
    byActionRaw && typeof byActionRaw === 'object' && !Array.isArray(byActionRaw)
      ? (byActionRaw as Record<string, number>)
      : {};
  const hourly = Array.isArray(hourlyRaw)
    ? hourlyRaw.map((row) => {
        const bucket = row as Record<string, unknown>;
        return {
          hourStart: String(bucket.hourStart ?? bucket.HourStart ?? ''),
          count: Number(bucket.count ?? bucket.Count ?? 0),
        };
      })
    : [];

  return {
    range: String(raw.range ?? raw.Range ?? ''),
    from: String(raw.from ?? raw.From ?? ''),
    to: String(raw.to ?? raw.To ?? ''),
    eventsTotal: Number(raw.eventsTotal ?? raw.EventsTotal ?? 0),
    byAction,
    hourly,
  };
}

const DASHBOARD_SUMMARY_CACHE_TTL_MS = 60_000;

interface DashboardSummaryCacheEntry {
  key: string;
  summary: SecEventDashboardSummary;
  fetchedAt: number;
}

let dashboardSummaryCache: DashboardSummaryCacheEntry | null = null;
let dashboardSummaryInflight: Promise<SecEventDashboardSummary> | null = null;
let dashboardSummaryInflightKey: string | null = null;

function dashboardSummaryCacheKey(options?: {
  rangeHours?: number;
  excludeUnknown?: boolean;
}): string {
  const rangeHours = options?.rangeHours ?? 24;
  const excludeUnknown = options?.excludeUnknown !== false;
  return `${rangeHours}:${excludeUnknown}`;
}

export function invalidateSecEventDashboardSummaryCache(): void {
  dashboardSummaryCache = null;
}

export async function secEventDashboardSummary(options?: {
  rangeHours?: number;
  excludeUnknown?: boolean;
}): Promise<SecEventDashboardSummary> {
  const cacheKey = dashboardSummaryCacheKey(options);
  const now = Date.now();

  if (
    dashboardSummaryCache
    && dashboardSummaryCache.key === cacheKey
    && now - dashboardSummaryCache.fetchedAt < DASHBOARD_SUMMARY_CACHE_TTL_MS
  ) {
    return dashboardSummaryCache.summary;
  }

  if (dashboardSummaryInflight && dashboardSummaryInflightKey === cacheKey) {
    return dashboardSummaryInflight;
  }

  dashboardSummaryInflightKey = cacheKey;
  dashboardSummaryInflight = (async () => {
    const qs = buildQuery({
      rangeHours: options?.rangeHours ?? 24,
      excludeUnknown: options?.excludeUnknown ?? true,
    });
    const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events/dashboard-summary${qs}`, {
      method: 'GET',
      headers: await authHeaders(),
    });
    const summary = normalizeDashboardSummary(raw);
    dashboardSummaryCache = { key: cacheKey, summary, fetchedAt: Date.now() };
    return summary;
  })();

  try {
    return await dashboardSummaryInflight;
  } finally {
    dashboardSummaryInflight = null;
    dashboardSummaryInflightKey = null;
  }
}
