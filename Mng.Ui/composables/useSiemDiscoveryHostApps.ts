import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';
import {
  preferredSecEventSearchTerm,
  secEventMatchesDiscoveryHost,
} from '@/utils/siemDiscoveryHostMatch';

export const DISCOVERY_APPS_STALE_MS = 5 * 60 * 1000;

export interface DiscoveryWatchTarget {
  kind: 'service' | 'application' | string;
  name: string;
  displayName?: string | null;
  health?: string | null;
  statusText?: string | null;
  restartAllowed?: boolean | null;
  instanceCount?: number | null;
  minCount?: number | null;
  lastRestartOk?: boolean | null;
  lastRestartAtUtc?: string | null;
  restartAttemptCount?: number | null;
}

export interface DiscoveryHostAppsSnapshot {
  targets: DiscoveryWatchTarget[];
  healthyCount: number | null;
  unhealthyCount: number | null;
  serviceCount: number | null;
  applicationCount: number | null;
  at: number | null;
}

export interface DiscoveryWatchActivityItem {
  id: string;
  at: number;
  timestamp: string;
  action: string;
  watchKind: string;
  name: string;
  displayName?: string | null;
  detail?: string | null;
  severity?: string | null;
}

export interface DiscoveryWatchActivitySnapshot {
  items: DiscoveryWatchActivityItem[];
  at: number | null;
}

function fromHours(hours: number): string {
  return new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
}

function asNumber(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string' && v.trim()) {
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

function asString(v: unknown): string | null {
  if (typeof v === 'string') {
    const t = v.trim();
    return t || null;
  }
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  return null;
}

function asBool(v: unknown): boolean | null {
  if (typeof v === 'boolean') return v;
  return null;
}

function itemTs(item: SecEventListItem): number | null {
  const ms = Date.parse(item.timestamp || item.ingestedAt || '');
  return Number.isFinite(ms) ? ms : null;
}

function parseTargets(raw: unknown): DiscoveryWatchTarget[] {
  if (!Array.isArray(raw)) return [];
  const out: DiscoveryWatchTarget[] = [];
  for (const row of raw) {
    if (!row || typeof row !== 'object') continue;
    const r = row as Record<string, unknown>;
    const name = asString(r.name);
    if (!name) continue;
    const kind = asString(r.kind) || 'service';
    out.push({
      kind,
      name,
      displayName: asString(r.displayName),
      health: asString(r.health),
      statusText: asString(r.statusText),
      restartAllowed: asBool(r.restartAllowed),
      instanceCount: asNumber(r.instanceCount),
      minCount: asNumber(r.minCount),
      lastRestartOk: asBool(r.lastRestartOk),
      lastRestartAtUtc: asString(r.lastRestartAtUtc),
      restartAttemptCount: asNumber(r.restartAttemptCount),
    });
  }
  return out.sort((a, b) => {
    const ka = a.kind.localeCompare(b.kind);
    if (ka !== 0) return ka;
    return a.name.localeCompare(b.name);
  });
}

function emptySnapshot(): DiscoveryHostAppsSnapshot {
  return {
    targets: [],
    healthyCount: null,
    unhealthyCount: null,
    serviceCount: null,
    applicationCount: null,
    at: null,
  };
}

/**
 * Latest watch.inventory for a discovery host (services + applications).
 */
type HostHints = Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null | undefined;

export async function fetchDiscoveryHostApps(
  hostname: string,
  options?: { from?: string; to?: string; host?: HostHints },
): Promise<DiscoveryHostAppsSnapshot> {
  const hostName = hostname.trim();
  if (!hostName) return emptySnapshot();

  const hostHints = options?.host ?? { hostname: hostName, ip: hostName, agent: null };
  const res = await secEventQuery({
    from: options?.from || fromHours(24),
    to: options?.to,
    sourceType: 'metric',
    eventAction: 'watch.inventory',
    excludeUnknown: false,
    search: preferredSecEventSearchTerm(hostName, hostHints),
    limit: 20,
  });

  const items = (res.items ?? []).filter((i) =>
    secEventMatchesDiscoveryHost(i, hostName, hostHints),
  );
  const hit = items[0];
  if (!hit) return emptySnapshot();

  const fields = hit.fields ?? {};
  return {
    targets: parseTargets(fields.targets),
    healthyCount: asNumber(fields.healthyCount),
    unhealthyCount: asNumber(fields.unhealthyCount),
    serviceCount: asNumber(fields.serviceCount),
    applicationCount: asNumber(fields.applicationCount),
    at: itemTs(hit),
  };
}

export function hostWatchEventsLink(
  hostname: string,
  host?: HostHints,
): string {
  const q = new URLSearchParams();
  q.set('sourceType', 'metric');
  q.set('eventAction', 'watch.inventory');
  q.set('timeRange', '24h');
  const term = preferredSecEventSearchTerm(
    hostname,
    host ?? { hostname, ip: hostname, agent: null },
  );
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
}

/** Deep-link for service/app watch transitions (not inventory snapshots). */
export function hostWatchActivityEventsLink(
  hostname: string,
  host?: HostHints,
): string {
  const q = new URLSearchParams();
  q.set('timeRange', '24h');
  const term = preferredSecEventSearchTerm(
    hostname,
    host ?? { hostname, ip: hostname, agent: null },
  );
  if (term) q.set('search', term);
  // Events UI is single-action; omit eventAction so host search covers all watch moves.
  return `/apps/siem-center/events?${q.toString()}`;
}

export function watchHealthTone(health?: string | null): 'success' | 'warning' | 'error' | 'info' {
  const h = (health || '').toLowerCase();
  if (h === 'running' || h === 'healthy') return 'success';
  if (h === 'missing' || h === 'notrunning' || h === 'stopped') return 'warning';
  if (h === 'error' || h === 'failed') return 'error';
  return 'info';
}

export function watchActivityTone(
  action: string,
): 'success' | 'warning' | 'error' | 'info' {
  const a = (action || '').toLowerCase();
  if (a.endsWith('.recovered') || a.endsWith('.restart.ok')) return 'success';
  if (a.endsWith('.failed') || a.endsWith('.missing') || a.endsWith('.restart.failed')) return 'error';
  if (a.includes('restart') || a.includes('skipped')) return 'warning';
  return 'info';
}

/**
 * Host watch transitions + restart attempts.
 * Sources: service-watch / app-watch (not watch.inventory snapshots).
 */
export async function fetchDiscoveryHostWatchActivity(
  hostname: string,
  options?: { from?: string; to?: string; limit?: number; host?: HostHints },
): Promise<DiscoveryWatchActivitySnapshot> {
  const hostName = hostname.trim();
  if (!hostName) return emptyActivity();

  const hostHints = options?.host ?? { hostname: hostName, ip: hostName, agent: null };
  const base = {
    from: options?.from || fromHours(24),
    to: options?.to,
    excludeUnknown: false,
    search: preferredSecEventSearchTerm(hostName, hostHints),
    limit: options?.limit ?? 80,
  } as const;

  const [svcRes, appRes] = await Promise.all([
    secEventQuery({ ...base, sourceType: 'service-watch' }),
    secEventQuery({ ...base, sourceType: 'app-watch' }),
  ]);

  const merged = [...(svcRes.items ?? []), ...(appRes.items ?? [])]
    .filter(
      (i) =>
        secEventMatchesDiscoveryHost(i, hostName, hostHints)
        && isWatchActivityAction(i.eventAction),
    )
    .map(toActivityItem)
    .filter((x): x is DiscoveryWatchActivityItem => x != null)
    .sort((a, b) => b.at - a.at);

  // Dedupe by id (same event shouldn't appear twice)
  const seen = new Set<string>();
  const items: DiscoveryWatchActivityItem[] = [];
  for (const row of merged) {
    if (seen.has(row.id)) continue;
    seen.add(row.id);
    items.push(row);
  }

  return { items, at: items[0]?.at ?? null };
}

function emptyActivity(): DiscoveryWatchActivitySnapshot {
  return { items: [], at: null };
}

function isWatchActivityAction(action: string | null | undefined): boolean {
  const a = (action || '').trim().toLowerCase();
  if (!a) return false;
  return (
    a.startsWith('service.')
    || a.startsWith('app.')
  ) && a !== 'watch.inventory';
}

function toActivityItem(item: SecEventListItem): DiscoveryWatchActivityItem | null {
  const at = itemTs(item);
  if (at == null) return null;
  const fields = item.fields ?? {};
  const action = (item.eventAction || asString(fields['event.action']) || '').trim();
  if (!isWatchActivityAction(action)) return null;

  const watchKind =
    asString(fields.watchKind)
    || (action.startsWith('app.') ? 'application' : 'service');

  const name =
    asString(fields.serviceName)
    || asString(fields.processName)
    || asString(fields.name)
    || '—';

  const detailParts: string[] = [];
  const status = asString(fields.status);
  if (status) detailParts.push(status);
  const transition = asString(fields.transition);
  if (transition) detailParts.push(transition);
  const err = asString(fields.error);
  if (err) detailParts.push(err);
  const attempt = asNumber(fields.restartAttempt);
  const maxAttempts = asNumber(fields.restartMaxAttempts);
  if (attempt != null) {
    detailParts.push(
      maxAttempts != null ? `attempt ${attempt}/${maxAttempts}` : `attempt ${attempt}`,
    );
  }
  const instances = asNumber(fields.instanceCount);
  const minCount = asNumber(fields.minCount);
  if (instances != null && minCount != null) {
    detailParts.push(`${instances}/${minCount}`);
  }

  return {
    id: item.id || `${at}-${action}-${name}`,
    at,
    timestamp: item.timestamp || new Date(at).toISOString(),
    action,
    watchKind,
    name,
    displayName: asString(fields.displayName),
    detail: detailParts.length ? detailParts.join(' · ') : null,
    severity: asString(item.eventOutcome) || asString(fields.severity),
  };
}
