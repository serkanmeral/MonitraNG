import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

export interface DiscoveryHostDto {
  id: string;
  hostname: string;
  ip: string;
  osHint?: string | null;
  sources: string[];
  samAccountName: string;
  lastSeenFromAd?: string | null;
}

export interface DiscoveryHostListResponse {
  domainId: string;
  total: number;
  items: DiscoveryHostDto[];
}

export interface DiscoverySummaryResponse {
  domainId: string;
  totalHosts: number;
  bySource: Record<string, number>;
  lastSyncAt?: string | null;
  lastSyncStatus: string;
  lastSyncError?: string | null;
  lastSyncStats: { pulled: number; upserted: number; durationMs: number };
}

export interface DiscoverySyncResponse {
  runId: string;
  status: string;
  pulled: number;
  upserted: number;
  durationMs: number;
  error?: string | null;
}

async function authHeaders(): Promise<Record<string, string>> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // BFF returns 401 if cookie/token missing.
  }

  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
  };
  if (authStore.domainName) {
    headers['X-Domain-Name'] = authStore.domainName;
  }
  const token = getAccessToken();
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

function normalizeHost(raw: Record<string, unknown>): DiscoveryHostDto {
  const sourcesRaw = (raw.sources ?? raw.Sources ?? []) as unknown[];
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    hostname: String(raw.hostname ?? raw.Hostname ?? ''),
    ip: String(raw.ip ?? raw.Ip ?? ''),
    osHint: (raw.osHint ?? raw.OsHint ?? null) as string | null,
    sources: Array.isArray(sourcesRaw) ? sourcesRaw.map(String) : [],
    samAccountName: String(raw.samAccountName ?? raw.SamAccountName ?? ''),
    lastSeenFromAd: (raw.lastSeenFromAd ?? raw.LastSeenFromAd ?? null) as string | null,
  };
}

/** Live discovery hosts from MngLogCollector via Nuxt BFF. */
export async function fetchDiscoveryHosts(params?: {
  domainId?: string;
  q?: string;
  source?: string;
  limit?: number;
}): Promise<DiscoveryHostListResponse> {
  const authStore = useAuthStore();
  const domainId = params?.domainId || authStore.domainName;
  if (!domainId) {
    throw new Error('domainId yok (oturum domain bilgisi).');
  }

  const query: Record<string, string> = {
    domainId,
    limit: String(params?.limit ?? 1000),
  };
  if (params?.q) query.q = params.q;
  if (params?.source) query.source = params.source;

  const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/hosts', {
    headers: await authHeaders(),
    query,
  });

  const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
  return {
    domainId: String(raw.domainId ?? raw.DomainId ?? domainId),
    total: Number(raw.total ?? raw.Total ?? 0),
    items: Array.isArray(itemsRaw) ? itemsRaw.map(normalizeHost) : [],
  };
}

export async function fetchDiscoverySummary(domainId?: string): Promise<DiscoverySummaryResponse> {
  const authStore = useAuthStore();
  const id = domainId || authStore.domainName;
  if (!id) throw new Error('domainId yok.');

  const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/summary', {
    headers: await authHeaders(),
    query: { domainId: id },
  });

  const stats = (raw.lastSyncStats ?? raw.LastSyncStats ?? {}) as Record<string, unknown>;
  const bySource = (raw.bySource ?? raw.BySource ?? {}) as Record<string, number>;

  return {
    domainId: String(raw.domainId ?? raw.DomainId ?? id),
    totalHosts: Number(raw.totalHosts ?? raw.TotalHosts ?? 0),
    bySource,
    lastSyncAt: (raw.lastSyncAt ?? raw.LastSyncAt ?? null) as string | null,
    lastSyncStatus: String(raw.lastSyncStatus ?? raw.LastSyncStatus ?? 'never'),
    lastSyncError: (raw.lastSyncError ?? raw.LastSyncError ?? null) as string | null,
    lastSyncStats: {
      pulled: Number(stats.pulled ?? stats.Pulled ?? 0),
      upserted: Number(stats.upserted ?? stats.Upserted ?? 0),
      durationMs: Number(stats.durationMs ?? stats.DurationMs ?? 0),
    },
  };
}

export async function triggerDiscoverySync(domainId?: string): Promise<DiscoverySyncResponse> {
  const authStore = useAuthStore();
  const id = domainId || authStore.domainName;
  if (!id) throw new Error('domainId yok.');

  const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/sync', {
    method: 'POST',
    headers: await authHeaders(),
    body: { domainId: id, source: 'ad' },
  });

  return {
    runId: String(raw.runId ?? raw.RunId ?? ''),
    status: String(raw.status ?? raw.Status ?? ''),
    pulled: Number(raw.pulled ?? raw.Pulled ?? 0),
    upserted: Number(raw.upserted ?? raw.Upserted ?? 0),
    durationMs: Number(raw.durationMs ?? raw.DurationMs ?? 0),
    error: (raw.error ?? raw.Error ?? null) as string | null,
  };
}
