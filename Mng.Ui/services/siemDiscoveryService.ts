import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

export interface DiscoveryHostDto {
  id: string;
  hostname: string;
  ip: string;
  osHint?: string | null;
  openPorts?: number[];
  deviceRoleHint?: string | null;
  identityConfidence?: string | null;
  identitySummary?: string | null;
  httpTitle?: string | null;
  tlsCommonName?: string | null;
  sshBanner?: string | null;
  subnetCidr?: string | null;
  siteLabel?: string | null;
  vlanName?: string | null;
  sources: string[];
  samAccountName: string;
  lastSeenFromAd?: string | null;
}

export interface DiscoveryPrefixDto {
  cidr: string;
  label: string;
  vlanName?: string | null;
}

export interface DiscoveryHostListResponse {
  domainId: string;
  total: number;
  items: DiscoveryHostDto[];
  prefixes?: DiscoveryPrefixDto[];
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
  const portsRaw = (raw.openPorts ?? raw.OpenPorts ?? []) as unknown[];
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    hostname: String(raw.hostname ?? raw.Hostname ?? ''),
    ip: String(raw.ip ?? raw.Ip ?? ''),
    osHint: (raw.osHint ?? raw.OsHint ?? null) as string | null,
    openPorts: Array.isArray(portsRaw)
      ? portsRaw.map((p) => Number(p)).filter((n) => Number.isFinite(n))
      : [],
    deviceRoleHint: (raw.deviceRoleHint ?? raw.DeviceRoleHint ?? null) as string | null,
    identityConfidence: (raw.identityConfidence ?? raw.IdentityConfidence ?? null) as string | null,
    identitySummary: (raw.identitySummary ?? raw.IdentitySummary ?? null) as string | null,
    httpTitle: (raw.httpTitle ?? raw.HttpTitle ?? null) as string | null,
    tlsCommonName: (raw.tlsCommonName ?? raw.TlsCommonName ?? null) as string | null,
    sshBanner: (raw.sshBanner ?? raw.SshBanner ?? null) as string | null,
    subnetCidr: (raw.subnetCidr ?? raw.SubnetCidr ?? null) as string | null,
    siteLabel: (raw.siteLabel ?? raw.SiteLabel ?? null) as string | null,
    vlanName: (raw.vlanName ?? raw.VlanName ?? null) as string | null,
    sources: Array.isArray(sourcesRaw) ? sourcesRaw.map(String) : [],
    samAccountName: String(raw.samAccountName ?? raw.SamAccountName ?? ''),
    lastSeenFromAd: (raw.lastSeenFromAd ?? raw.LastSeenFromAd ?? null) as string | null,
  };
}

function normalizePrefix(raw: Record<string, unknown>): DiscoveryPrefixDto {
  return {
    cidr: String(raw.cidr ?? raw.Cidr ?? ''),
    label: String(raw.label ?? raw.Label ?? ''),
    vlanName: (raw.vlanName ?? raw.VlanName ?? null) as string | null,
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
  const prefixesRaw = (raw.prefixes ?? raw.Prefixes ?? []) as Record<string, unknown>[];
  return {
    domainId: String(raw.domainId ?? raw.DomainId ?? domainId),
    total: Number(raw.total ?? raw.Total ?? 0),
    items: Array.isArray(itemsRaw) ? itemsRaw.map(normalizeHost) : [],
    prefixes: Array.isArray(prefixesRaw)
      ? prefixesRaw.map(normalizePrefix).filter((p) => !!p.cidr)
      : [],
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

export interface DiscoveryClearResponse {
  domainId: string;
  status: string;
  source?: string | null;
  deleted: number;
  error?: string | null;
}

export interface DiscoveryPrefixesResponse {
  domainId: string;
  source: string;
  prefixes: DiscoveryPrefixDto[];
  error?: string | null;
}

export async function fetchDiscoveryPrefixes(domainId?: string): Promise<DiscoveryPrefixesResponse> {
  const authStore = useAuthStore();
  const id = domainId || authStore.domainName;
  if (!id) throw new Error('domainId yok.');

  const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/prefixes', {
    headers: await authHeaders(),
    query: { domainId: id },
  });
  const prefixesRaw = (raw.prefixes ?? raw.Prefixes ?? []) as Record<string, unknown>[];
  return {
    domainId: String(raw.domainId ?? raw.DomainId ?? id),
    source: String(raw.source ?? raw.Source ?? 'config'),
    prefixes: Array.isArray(prefixesRaw)
      ? prefixesRaw.map(normalizePrefix).filter((p) => !!p.cidr)
      : [],
    error: (raw.error ?? raw.Error ?? null) as string | null,
  };
}

export async function putDiscoveryPrefixes(
  prefixes: DiscoveryPrefixDto[],
  domainId?: string,
): Promise<DiscoveryPrefixesResponse> {
  const authStore = useAuthStore();
  const id = domainId || authStore.domainName;
  if (!id) throw new Error('domainId yok.');

  const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/prefixes', {
    method: 'PUT',
    headers: await authHeaders(),
    query: { domainId: id },
    body: { prefixes },
  });
  const prefixesRaw = (raw.prefixes ?? raw.Prefixes ?? []) as Record<string, unknown>[];
  return {
    domainId: String(raw.domainId ?? raw.DomainId ?? id),
    source: String(raw.source ?? raw.Source ?? 'mongo'),
    prefixes: Array.isArray(prefixesRaw)
      ? prefixesRaw.map(normalizePrefix).filter((p) => !!p.cidr)
      : [],
    error: (raw.error ?? raw.Error ?? null) as string | null,
  };
}

/** Clear discovery hosts. source: omit/empty = all, 'scan' | 'ad' = filter. */
export async function clearDiscoveryHosts(params?: {
  domainId?: string;
  source?: 'scan' | 'ad' | '';
}): Promise<DiscoveryClearResponse> {
  const authStore = useAuthStore();
  const id =
    params?.domainId
    || authStore.domainName
    || (authStore.userInfo as { domain_name?: string } | null)?.domain_name;
  if (!id) throw new Error('domainId yok (oturum domain bilgisi).');

  const source = params?.source || undefined;
  try {
    const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/hosts/clear', {
      method: 'POST',
      headers: await authHeaders(),
      query: { domainId: id },
      body: {
        domainId: id,
        ...(source ? { source } : {}),
      },
    });

    return {
      domainId: String(raw.domainId ?? raw.DomainId ?? id),
      status: String(raw.status ?? raw.Status ?? ''),
      source: (raw.source ?? raw.Source ?? null) as string | null,
      deleted: Number(raw.deleted ?? raw.Deleted ?? 0),
      error: (raw.error ?? raw.Error ?? null) as string | null,
    };
  } catch (error: unknown) {
    const status = (error as { statusCode?: number; status?: number })?.statusCode
      ?? (error as { status?: number })?.status;
    const data = (error as { data?: Record<string, unknown> })?.data;
    const msg = scanApiErrorMessage(error, 'Discovery temizlenemedi');
    if (status === 400 || status === 409) {
      return {
        domainId: String(data?.domainId ?? data?.DomainId ?? id),
        status: 'error',
        source: (data?.source ?? data?.Source ?? null) as string | null,
        deleted: Number(data?.deleted ?? data?.Deleted ?? 0),
        error: msg,
      };
    }
    throw new Error(msg);
  }
}

export interface DiscoveryScanStartResponse {
  runId: string;
  status: string;
  totalTargets: number;
  error?: string | null;
}

export interface DiscoveryScanStatusResponse {
  runId: string;
  domainId: string;
  cidr: string;
  enrichWithAd: boolean;
  status: string;
  progressPercent: number;
  totalTargets: number;
  probed: number;
  foundAlive: number;
  foundWindows: number;
  foundLinux: number;
  foundUnknown: number;
  upserted: number;
  error?: string | null;
  createdAt?: string;
  startedAt?: string | null;
  completedAt?: string | null;
}

function normalizeScanStatus(raw: Record<string, unknown>): DiscoveryScanStatusResponse {
  return {
    runId: String(raw.runId ?? raw.RunId ?? ''),
    domainId: String(raw.domainId ?? raw.DomainId ?? ''),
    cidr: String(raw.cidr ?? raw.Cidr ?? ''),
    enrichWithAd: Boolean(raw.enrichWithAd ?? raw.EnrichWithAd ?? false),
    status: String(raw.status ?? raw.Status ?? ''),
    progressPercent: Number(raw.progressPercent ?? raw.ProgressPercent ?? 0),
    totalTargets: Number(raw.totalTargets ?? raw.TotalTargets ?? 0),
    probed: Number(raw.probed ?? raw.Probed ?? 0),
    foundAlive: Number(raw.foundAlive ?? raw.FoundAlive ?? 0),
    foundWindows: Number(raw.foundWindows ?? raw.FoundWindows ?? 0),
    foundLinux: Number(raw.foundLinux ?? raw.FoundLinux ?? 0),
    foundUnknown: Number(raw.foundUnknown ?? raw.FoundUnknown ?? 0),
    upserted: Number(raw.upserted ?? raw.Upserted ?? 0),
    error: (raw.error ?? raw.Error ?? null) as string | null,
    createdAt: (raw.createdAt ?? raw.CreatedAt ?? undefined) as string | undefined,
    startedAt: (raw.startedAt ?? raw.StartedAt ?? null) as string | null,
    completedAt: (raw.completedAt ?? raw.CompletedAt ?? null) as string | null,
  };
}

function scanApiErrorMessage(error: unknown, fallback: string): string {
  const e = error as {
    data?: { error?: string; Error?: string; statusMessage?: string };
    statusMessage?: string;
    message?: string;
  };
  return (
    e?.data?.error
    || e?.data?.Error
    || e?.data?.statusMessage
    || e?.statusMessage
    || e?.message
    || fallback
  );
}

export async function startDiscoveryScan(params: {
  cidr: string;
  enrichWithAd?: boolean;
  domainId?: string;
}): Promise<DiscoveryScanStartResponse> {
  const authStore = useAuthStore();
  const id =
    params.domainId
    || authStore.domainName
    || (authStore.userInfo as { domain_name?: string } | null)?.domain_name;
  if (!id) throw new Error('domainId yok (oturum domain bilgisi).');

  try {
    const raw = await $fetch<Record<string, unknown>>('/api/logcollector/v1/discovery/scan', {
      method: 'POST',
      headers: await authHeaders(),
      query: { domainId: id },
      body: {
        domainId: id,
        cidr: params.cidr,
        enrichWithAd: !!params.enrichWithAd,
      },
    });

    return {
      runId: String(raw.runId ?? raw.RunId ?? ''),
      status: String(raw.status ?? raw.Status ?? ''),
      totalTargets: Number(raw.totalTargets ?? raw.TotalTargets ?? 0),
      error: (raw.error ?? raw.Error ?? null) as string | null,
    };
  } catch (error: unknown) {
    const status = (error as { statusCode?: number; status?: number })?.statusCode
      ?? (error as { status?: number })?.status;
    const msg = scanApiErrorMessage(error, 'Tarama başlatılamadı');
    // Surface 400/409 body as a structured result when possible
    if (status === 400 || status === 409) {
      return {
        runId: String((error as { data?: { runId?: string } })?.data?.runId ?? ''),
        status: 'error',
        totalTargets: Number((error as { data?: { totalTargets?: number } })?.data?.totalTargets ?? 0),
        error: msg,
      };
    }
    throw new Error(msg);
  }
}

export async function fetchDiscoveryScanStatus(
  runId: string,
  domainId?: string,
): Promise<DiscoveryScanStatusResponse> {
  const authStore = useAuthStore();
  const id = domainId || authStore.domainName;
  if (!id) throw new Error('domainId yok.');

  const raw = await $fetch<Record<string, unknown>>(
    `/api/logcollector/v1/discovery/scan/${encodeURIComponent(runId)}`,
    {
      headers: await authHeaders(),
      query: { domainId: id },
    },
  );
  return normalizeScanStatus(raw);
}

export async function cancelDiscoveryScan(
  runId: string,
  domainId?: string,
): Promise<DiscoveryScanStatusResponse> {
  const authStore = useAuthStore();
  const id = domainId || authStore.domainName;
  if (!id) throw new Error('domainId yok.');

  const raw = await $fetch<Record<string, unknown>>(
    `/api/logcollector/v1/discovery/scan/${encodeURIComponent(runId)}/cancel`,
    {
      method: 'POST',
      headers: await authHeaders(),
      body: { domainId: id },
      query: { domainId: id },
    },
  );
  return normalizeScanStatus(raw);
}
