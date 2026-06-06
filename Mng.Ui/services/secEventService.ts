import type {
  SecEventListItem,
  SecEventQuery,
  SecEventQueryResponse,
  SecEventDashboardSummary,
} from '@/types/apps/secEvent';
import { useAuthStore } from '@/stores/auth';

function domainHeaders(): Record<string, string> {
  const auth = useAuthStore();
  const headers: Record<string, string> = {};
  if (auth.domainName) {
    headers['X-Domain-Name'] = auth.domainName;
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
    headers: domainHeaders(),
  });
  return normalizeQueryResponse(raw);
}

export async function secEventGet(id: string): Promise<SecEventListItem> {
  const raw = await $fetch<Record<string, unknown>>(`/api/reactor/v1/sec-events/${encodeURIComponent(id)}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
  return normalizeListItem(raw);
}

export async function secEventDashboardSummary(options?: {
  rangeHours?: number;
  excludeUnknown?: boolean;
}): Promise<SecEventDashboardSummary> {
  const qs = buildQuery({
    rangeHours: options?.rangeHours ?? 24,
    excludeUnknown: options?.excludeUnknown ?? true,
  });
  return await $fetch<SecEventDashboardSummary>(`/api/reactor/v1/sec-events/dashboard-summary${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}
