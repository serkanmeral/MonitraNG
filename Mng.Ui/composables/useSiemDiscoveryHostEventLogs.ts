import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';

export const DISCOVERY_EVENTLOG_STALE_MS = 15 * 60 * 1000;

export interface DiscoveryHostEventLogItem {
  id: string;
  at: number;
  timestamp: string;
  channel: string;
  packageName: string | null;
  eventId: string | null;
  provider: string | null;
  level: string | null;
  message: string | null;
  action: string | null;
  /** Host reported on the sec-event (for detail fallback). */
  sourceHost?: string | null;
  /** List-row blobs so detail UI works when GetById cannot resolve slash-ids. */
  rawPreview?: string | null;
  eventAction?: string | null;
  fields?: Record<string, unknown> | null;
}

export interface DiscoveryHostEventLogSnapshot {
  items: DiscoveryHostEventLogItem[];
  at: number | null;
}

function fromHours(hours: number): string {
  return new Date(Date.now() - hours * 60 * 60 * 1000).toISOString();
}

function shortHostKey(hostname: string): string {
  const h = hostname.trim().toLowerCase();
  return h.split('.')[0] || h;
}

function asString(v: unknown): string | null {
  if (typeof v === 'string') {
    const t = v.trim();
    return t || null;
  }
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  return null;
}

function asNumber(v: unknown): number | null {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string' && v.trim()) {
    const n = Number(v);
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

function itemTs(item: SecEventListItem): number | null {
  const ms = Date.parse(item.timestamp || item.ingestedAt || '');
  return Number.isFinite(ms) ? ms : null;
}

function matchesHost(item: SecEventListItem, hostname: string): boolean {
  const want = shortHostKey(hostname);
  if (!want) return false;

  const candidates = [
    item.sourceHost,
    asString(item.fields?.machine),
    asString(item.fields?.['host.name']),
  ];

  for (const raw of candidates) {
    const src = (raw || '').trim().toLowerCase();
    if (!src) continue;
    if (src === want || shortHostKey(src) === want || src.includes(want) || want.includes(shortHostKey(src))) {
      return true;
    }
  }
  return false;
}

function emptySnapshot(): DiscoveryHostEventLogSnapshot {
  return { items: [], at: null };
}

function toItem(item: SecEventListItem): DiscoveryHostEventLogItem | null {
  const at = itemTs(item);
  if (at == null) return null;
  const fields = item.fields ?? {};

  const eventId =
    item.eventCode
    || asString(fields.eventId)
    || (asNumber(fields.eventId) != null ? String(asNumber(fields.eventId)) : null);

  const channel =
    asString(fields.channel)
    || '—';

  const message =
    asString(item.rawPreview)
    || asString(fields.message)
    || null;

  const action = (item.eventAction || '').trim();
  const actionUseful =
    action
    && action.toLowerCase() !== 'unknown'
    && action !== message
    && !/^EventID\s+\d+/i.test(action)
      ? action
      : null;

  return {
    id: item.id || `${at}-${channel}-${eventId || 'x'}`,
    at,
    timestamp: item.timestamp || new Date(at).toISOString(),
    channel,
    packageName: asString(item.sourceProduct) || asString(fields.package),
    eventId,
    provider: asString(fields.provider),
    level: asString(item.eventOutcome) || asString(fields.severity) || asString(fields.level),
    message,
    action: actionUseful,
    sourceHost: item.sourceHost ?? null,
    rawPreview: item.rawPreview ?? null,
    eventAction: item.eventAction || null,
    fields: item.fields ?? null,
  };
}

/**
 * Windows Event Log rows for a discovery host.
 */
export async function fetchDiscoveryHostEventLogs(
  hostname: string,
  options?: { from?: string; to?: string; limit?: number },
): Promise<DiscoveryHostEventLogSnapshot> {
  const host = hostname.trim();
  if (!host) return emptySnapshot();

  const res = await secEventQuery({
    from: options?.from || fromHours(24),
    to: options?.to,
    sourceType: 'windows-eventlog',
    excludeUnknown: false,
    search: shortHostKey(host),
    limit: options?.limit ?? 100,
  });

  const items = (res.items ?? [])
    .filter((i) => matchesHost(i, host))
    .map(toItem)
    .filter((x): x is DiscoveryHostEventLogItem => x != null)
    .sort((a, b) => b.at - a.at);

  const seen = new Set<string>();
  const deduped: DiscoveryHostEventLogItem[] = [];
  for (const row of items) {
    if (seen.has(row.id)) continue;
    seen.add(row.id);
    deduped.push(row);
  }

  return {
    items: deduped,
    at: deduped[0]?.at ?? null,
  };
}

export function hostEventLogEventsLink(hostname: string): string {
  const q = new URLSearchParams();
  q.set('sourceType', 'windows-eventlog');
  q.set('timeRange', '24h');
  const term = shortHostKey(hostname);
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
}

export function eventLogLevelTone(
  level?: string | null,
): 'success' | 'warning' | 'error' | 'info' {
  const l = (level || '').toLowerCase();
  if (l === 'failure' || l === 'error' || l === 'critical' || l === '2' || l === '1') return 'error';
  if (l === 'warning' || l === 'unknown' || l === '3') return 'warning';
  if (l === 'success' || l === 'info' || l === 'verbose' || l === '4' || l === '5') return 'info';
  return 'info';
}

export function channelFilterKey(channel: string): string {
  const c = channel.trim().toLowerCase();
  if (c === 'security') return 'Security';
  if (c === 'system') return 'System';
  if (c === 'application') return 'Application';
  if (c.includes('powershell')) return 'PowerShell';
  if (c.includes('terminalservices') || c.includes('localsessionmanager')) return 'RDP';
  return 'Other';
}
