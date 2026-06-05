import type { SecEventListItem, SecEventQuery, SecEventQueryResponse } from '@/types/apps/secEvent';
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
  return await $fetch<SecEventQueryResponse>(`/api/reactor/v1/sec-events${qs}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}

export async function secEventGet(id: string): Promise<SecEventListItem> {
  return await $fetch<SecEventListItem>(`/api/reactor/v1/sec-events/${encodeURIComponent(id)}`, {
    method: 'GET',
    headers: domainHeaders(),
  });
}
