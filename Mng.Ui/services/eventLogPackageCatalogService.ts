import type {
  EventLogPackageCatalogResponse,
  EventLogPackageManageListResponse,
  EventLogPackageManageItem,
  EventLogPackageUpsertPayload,
  EventLogChannelDictionary,
  EventLogPackagePreset,
  EventLogHostAssignment,
  EventLogHostAssignmentUpsertPayload,
} from '@/types/apps/eventLogPackageCatalog';
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

function normalizeIds(raw: unknown): number[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((n) => Number(n)).filter((n) => Number.isFinite(n) && n > 0);
}

function normalizeSelectionMode(raw: unknown): 'selected' | 'all' {
  return String(raw ?? '').trim().toLowerCase() === 'all' ? 'all' : 'selected';
}

function normalizePackage(raw: Record<string, unknown>) {
  return {
    name: String(raw.name ?? raw.Name ?? ''),
    channel: String(raw.channel ?? raw.Channel ?? ''),
    selectionMode: normalizeSelectionMode(raw.selectionMode ?? raw.SelectionMode),
    eventIds: normalizeIds(raw.eventIds ?? raw.EventIds),
    excludedEventIds: normalizeIds(raw.excludedEventIds ?? raw.ExcludedEventIds),
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

function normalizeManageItem(raw: Record<string, unknown>): EventLogPackageManageItem {
  const base = normalizePackage(raw);
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    ...base,
    isDefault: Boolean(raw.isDefault ?? raw.IsDefault),
    updatedAtUtc: String(raw.updatedAtUtc ?? raw.UpdatedAtUtc ?? ''),
  };
}

/** Agent-facing catalog (defaults + optional). */
export async function fetchEventLogPackageCatalog(): Promise<EventLogPackageCatalogResponse> {
  const raw = await $fetch<Record<string, unknown>>(
    '/api/logcollector/v1/policy/eventlog-packages',
    { headers: await authHeaders() },
  );
  return normalizeCatalog(raw);
}

export async function fetchEventLogPackageManageList(): Promise<EventLogPackageManageListResponse> {
  const raw = await $fetch<Record<string, unknown>>(
    '/api/logcollector/v1/policy/eventlog-packages/manage',
    { headers: await authHeaders() },
  );
  const itemsRaw = (raw.items ?? raw.Items ?? []) as Record<string, unknown>[];
  return {
    version: String(raw.version ?? raw.Version ?? ''),
    publishedUtc: (raw.publishedUtc ?? raw.PublishedUtc)
      ? String(raw.publishedUtc ?? raw.PublishedUtc)
      : null,
    hasUnpublishedChanges: Boolean(raw.hasUnpublishedChanges ?? raw.HasUnpublishedChanges),
    items: Array.isArray(itemsRaw) ? itemsRaw.map(normalizeManageItem) : [],
  };
}

export async function fetchEventLogChannelDictionary(): Promise<EventLogChannelDictionary[]> {
  const raw = await $fetch<unknown>(
    '/api/logcollector/v1/policy/eventlog-packages/channels',
    { headers: await authHeaders() },
  );
  const list = Array.isArray(raw) ? raw : [];
  return list.map((row) => {
    const r = row as Record<string, unknown>;
    const knownRaw = (r.knownEventIds ?? r.KnownEventIds ?? []) as Record<string, unknown>[];
    return {
      channel: String(r.channel ?? r.Channel ?? ''),
      label: String(r.label ?? r.Label ?? ''),
      knownEventIds: Array.isArray(knownRaw)
        ? knownRaw.map((k) => ({
            id: Number(k.id ?? k.Id ?? 0),
            label: String(k.label ?? k.Label ?? ''),
          }))
        : [],
    };
  });
}

export async function fetchEventLogPackagePresets(): Promise<EventLogPackagePreset[]> {
  const raw = await $fetch<unknown>(
    '/api/logcollector/v1/policy/eventlog-packages/presets',
    { headers: await authHeaders() },
  );
  const list = Array.isArray(raw) ? raw : [];
  return list.map((row) => {
    const r = row as Record<string, unknown>;
    const idsRaw = (r.eventIds ?? r.EventIds ?? []) as unknown[];
    return {
      id: String(r.id ?? r.Id ?? ''),
      title: String(r.title ?? r.Title ?? ''),
      description: String(r.description ?? r.Description ?? ''),
      suggestedName: String(r.suggestedName ?? r.SuggestedName ?? ''),
      channel: String(r.channel ?? r.Channel ?? ''),
      isDefault: Boolean(r.isDefault ?? r.IsDefault),
      eventIds: Array.isArray(idsRaw)
        ? idsRaw.map((n) => Number(n)).filter((n) => Number.isFinite(n) && n > 0)
        : [],
    };
  });
}

function normalizeAssignment(raw: Record<string, unknown>): EventLogHostAssignment {
  const enabledRaw = (raw.enabledOptionalPackages ?? raw.EnabledOptionalPackages ?? []) as unknown[];
  const disabledRaw = (raw.disabledServerPackages ?? raw.DisabledServerPackages ?? []) as unknown[];
  const updated = raw.updatedAtUtc ?? raw.UpdatedAtUtc;
  return {
    hostname: String(raw.hostname ?? raw.Hostname ?? ''),
    hostKey: String(raw.hostKey ?? raw.HostKey ?? ''),
    enabledOptionalPackages: Array.isArray(enabledRaw)
      ? enabledRaw.map((x) => String(x)).filter(Boolean)
      : [],
    disabledServerPackages: Array.isArray(disabledRaw)
      ? disabledRaw.map((x) => String(x)).filter(Boolean)
      : [],
    updatedAtUtc: updated ? String(updated) : null,
  };
}

export async function fetchEventLogHostAssignment(hostname: string): Promise<EventLogHostAssignment> {
  const raw = await $fetch<Record<string, unknown>>(
    `/api/logcollector/v1/policy/eventlog-packages/assignments/${encodeURIComponent(hostname)}`,
    { headers: await authHeaders() },
  );
  return normalizeAssignment(raw);
}

export async function saveEventLogHostAssignment(
  hostname: string,
  payload: EventLogHostAssignmentUpsertPayload,
): Promise<EventLogHostAssignment> {
  const raw = await $fetch<Record<string, unknown>>(
    `/api/logcollector/v1/policy/eventlog-packages/assignments/${encodeURIComponent(hostname)}`,
    {
      method: 'PUT',
      headers: await authHeaders(),
      body: payload,
    },
  );
  return normalizeAssignment(raw);
}

export async function createEventLogPackage(
  payload: EventLogPackageUpsertPayload,
): Promise<EventLogPackageManageItem> {
  const raw = await $fetch<Record<string, unknown>>(
    '/api/logcollector/v1/policy/eventlog-packages',
    {
      method: 'POST',
      headers: await authHeaders(),
      body: payload,
    },
  );
  return normalizeManageItem(raw);
}

export async function updateEventLogPackage(
  name: string,
  payload: EventLogPackageUpsertPayload,
): Promise<EventLogPackageManageItem> {
  const raw = await $fetch<Record<string, unknown>>(
    `/api/logcollector/v1/policy/eventlog-packages/${encodeURIComponent(name)}`,
    {
      method: 'PUT',
      headers: await authHeaders(),
      body: payload,
    },
  );
  return normalizeManageItem(raw);
}

export async function deleteEventLogPackage(name: string): Promise<void> {
  await $fetch(`/api/logcollector/v1/policy/eventlog-packages/${encodeURIComponent(name)}`, {
    method: 'DELETE',
    headers: await authHeaders(),
  });
}

export async function publishEventLogPackageCatalog(): Promise<EventLogPackageCatalogResponse> {
  const raw = await $fetch<Record<string, unknown>>(
    '/api/logcollector/v1/policy/eventlog-packages/publish',
    {
      method: 'POST',
      headers: await authHeaders(),
      body: {},
    },
  );
  return normalizeCatalog(raw);
}
