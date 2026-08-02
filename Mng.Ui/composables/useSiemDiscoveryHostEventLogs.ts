import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type { SiemDiscoveryHost, SiemOsFamily } from '@/types/apps/siemDiscovery';
import {
  preferredSecEventSearchTerm,
  secEventMatchesDiscoveryHost,
  shortHostKey,
} from '@/utils/siemDiscoveryHostMatch';

export const DISCOVERY_EVENTLOG_STALE_MS = 15 * 60 * 1000;

export type HostEventLogSourceType = 'windows-eventlog' | 'linux-journal';

/** Resolve sec-event sourceType for host Event Log / Journal panels. */
export function resolveHostEventLogSourceType(
  osFamily?: SiemOsFamily | string | null,
): HostEventLogSourceType {
  const fam = (osFamily || '').toString().trim().toLowerCase();
  if (fam === 'linux') return 'linux-journal';
  return 'windows-eventlog';
}

export function isLinuxHostEventLog(
  osFamily?: SiemOsFamily | string | null,
): boolean {
  return resolveHostEventLogSourceType(osFamily) === 'linux-journal';
}

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

  // Prefer journal package name over agent product (mnglogs-agent).
  const packageName =
    asString(fields.package)
    || asString(fields.packageName)
    || asString(item.sourceProduct);

  const provider =
    asString(fields.provider)
    || asString(fields.identifier)
    || asString(fields.unit);

  return {
    id: item.id || `${at}-${channel}-${eventId || packageName || 'x'}`,
    at,
    timestamp: item.timestamp || new Date(at).toISOString(),
    channel,
    packageName,
    eventId: eventId || asString(fields['event.action']) || actionUseful,
    provider,
    level:
      asString(item.eventOutcome)
      || asString(fields.severity)
      || asString(fields.level)
      || asString(fields.priority),
    message,
    action: actionUseful,
    sourceHost: item.sourceHost ?? null,
    rawPreview: item.rawPreview ?? null,
    eventAction: item.eventAction || actionUseful || null,
    fields: item.fields ?? null,
  };
}

/**
 * Event Log (Windows) or journal (Linux) rows for a discovery host.
 */
export async function fetchDiscoveryHostEventLogs(
  hostname: string,
  options?: {
    from?: string;
    to?: string;
    limit?: number;
    osFamily?: SiemOsFamily | string | null;
    host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null;
  },
): Promise<DiscoveryHostEventLogSnapshot> {
  const hostName = hostname.trim();
  if (!hostName) return emptySnapshot();

  const sourceType = resolveHostEventLogSourceType(options?.osFamily);
  const hostHints = options?.host ?? { hostname: hostName, ip: hostName, agent: null };

  const res = await secEventQuery({
    from: options?.from || fromHours(24),
    to: options?.to,
    sourceType,
    excludeUnknown: false,
    search: preferredSecEventSearchTerm(hostName, hostHints),
    limit: options?.limit ?? 100,
  });

  const items = (res.items ?? [])
    .filter((i) => secEventMatchesDiscoveryHost(i, hostName, hostHints))
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

export function hostEventLogEventsLink(
  hostname: string,
  osFamily?: SiemOsFamily | string | null,
  host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null,
): string {
  const q = new URLSearchParams();
  q.set('sourceType', resolveHostEventLogSourceType(osFamily));
  q.set('timeRange', '24h');
  const term = preferredSecEventSearchTerm(
    hostname,
    host ?? { hostname, ip: hostname, agent: null },
  );
  if (term) q.set('search', term);
  return `/apps/siem-center/events?${q.toString()}`;
}

export { shortHostKey };

export function eventLogLevelTone(
  level?: string | null,
): 'success' | 'warning' | 'error' | 'info' {
  const l = (level || '').toLowerCase();
  if (l === 'failure' || l === 'error' || l === 'critical' || l === '2' || l === '1') return 'error';
  if (l === 'warning' || l === 'unknown' || l === '3') return 'warning';
  if (l === 'success' || l === 'info' || l === 'verbose' || l === '4' || l === '5') return 'info';
  // journald PRIORITY: 0-3 err, 4 warning
  if (l === '0' || l === 'emerg' || l === 'alert') return 'error';
  return 'info';
}

/** Bucket key for channel/unit pie + filters (Windows channels or journal packages). */
export function channelFilterKey(
  channel: string,
  packageName?: string | null,
): string {
  const pkg = (packageName || '').trim().toLowerCase();
  if (pkg === 'sshd' || pkg === 'ssh') return 'sshd';
  if (pkg === 'sudo') return 'sudo';
  if (pkg === 'unit-fail' || pkg === 'unit_fail') return 'unit-fail';

  const c = channel.trim().toLowerCase();
  if (!c || c === '—') return pkg || 'Other';
  if (c === 'security') return 'Security';
  if (c === 'system') return 'System';
  if (c === 'application') return 'Application';
  if (c.includes('powershell')) return 'PowerShell';
  if (c.includes('terminalservices') || c.includes('localsessionmanager')) return 'RDP';
  if (c.includes('sshd') || c === 'ssh.service' || c === 'sshd.service') return 'sshd';
  if (c.includes('sudo')) return 'sudo';
  if (pkg) return pkg;
  return 'Other';
}
