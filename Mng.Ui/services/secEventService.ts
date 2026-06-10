import type {
  SecEventListItem,
  SecEventQuery,
  SecEventQueryResponse,
  SecEventDashboardSummary,
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
  };
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
    eventAction: query.eventAction,
    srcIp: query.srcIp,
    actorUser: query.actorUser,
    search: query.search,
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

export async function secEventGet(id: string): Promise<SecEventListItem> {
  const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events/${encodeURIComponent(id)}`, {
    method: 'GET',
    headers: await authHeaders(),
  });
  return normalizeListItem(raw);
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

export async function secEventDashboardSummary(options?: {
  rangeHours?: number;
  excludeUnknown?: boolean;
}): Promise<SecEventDashboardSummary> {
  const qs = buildQuery({
    rangeHours: options?.rangeHours ?? 24,
    excludeUnknown: options?.excludeUnknown ?? true,
  });
  const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events/dashboard-summary${qs}`, {
    method: 'GET',
    headers: await authHeaders(),
  });
  return normalizeDashboardSummary(raw);
}
