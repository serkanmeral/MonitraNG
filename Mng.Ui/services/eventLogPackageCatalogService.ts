import type { EventLogPackageCatalogResponse } from '@/types/apps/eventLogPackageCatalog';
import { getAccessToken } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';

async function authHeaders(): Promise<Record<string, string>> {
  const authStore = useAuthStore();
  try {
    await authStore.ensureValidToken();
  } catch {
    // BFF returns 401 if cookie/token missing.
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

function normalizePackage(raw: Record<string, unknown>) {
  const idsRaw = (raw.eventIds ?? raw.EventIds ?? []) as unknown[];
  const eventIds = Array.isArray(idsRaw)
    ? idsRaw.map((n) => Number(n)).filter((n) => Number.isFinite(n))
    : [];
  return {
    name: String(raw.name ?? raw.Name ?? ''),
    channel: String(raw.channel ?? raw.Channel ?? ''),
    eventIds,
  };
}

function normalizeCatalog(raw: Record<string, unknown>): EventLogPackageCatalogResponse {
  const packagesRaw = (raw.packages ?? raw.Packages ?? []) as Record<string, unknown>[];
  const optionalRaw = (raw.optionalPackages ?? raw.OptionalPackages ?? []) as Record<
    string,
    unknown
  >[];
  return {
    version: String(raw.version ?? raw.Version ?? ''),
    source: String(raw.source ?? raw.Source ?? ''),
    generatedUtc: String(raw.generatedUtc ?? raw.GeneratedUtc ?? ''),
    packages: Array.isArray(packagesRaw) ? packagesRaw.map(normalizePackage) : [],
    optionalPackages: Array.isArray(optionalRaw) ? optionalRaw.map(normalizePackage) : [],
  };
}

/** Live catalog from MngLogCollector via Nuxt BFF. */
export async function fetchEventLogPackageCatalog(): Promise<EventLogPackageCatalogResponse> {
  const raw = await $fetch<Record<string, unknown>>(
    '/api/logcollector/v1/policy/eventlog-packages',
    { headers: await authHeaders() },
  );
  return normalizeCatalog(raw);
}
