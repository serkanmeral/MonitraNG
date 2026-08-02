import {
  diskUsedPercent,
  fetchDiscoveryHostMetrics,
  primaryDisk,
  type DiscoveryHostMetricsSnapshot,
  type MetricPoint,
} from '@/composables/useSiemDiscoveryHostMetrics';
import {
  fetchDiscoveryHostApps,
  fetchDiscoveryHostWatchActivity,
  type DiscoveryHostAppsSnapshot,
  type DiscoveryWatchActivitySnapshot,
} from '@/composables/useSiemDiscoveryHostApps';
import {
  channelFilterKey,
  fetchDiscoveryHostEventLogs,
  type DiscoveryHostEventLogItem,
  type DiscoveryHostEventLogSnapshot,
} from '@/composables/useSiemDiscoveryHostEventLogs';
import { secEventQuery } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';
import { preferredSecEventSearchTerm } from '@/utils/siemDiscoveryHostMatch';
import {
  isWindowsMachineAccount,
  parseWindowsRdpSessionMessage,
  parseWindowsSecurityLogonMessage,
  securityMessageFromEventFields,
  SESSION_HISTORY_EVENT_IDS,
  SESSION_HISTORY_RDP_EVENT_IDS,
} from '@/utils/windowsSecurityLogonParse';

export type HostAnalyticsTimeRange = '1h' | '6h' | '24h' | '7d' | 'custom';

export type HostRoleChip = 'dc' | 'sql' | 'memberServer' | 'workstation';

export interface HostAnalyticsRange {
  timeRange: HostAnalyticsTimeRange;
  from: string;
  to: string | undefined;
  fromMs: number;
  toMs: number;
}

export interface HostAnalyticsKpis {
  heartbeatAgeSec: number | null;
  cpuLast: number | null;
  cpuAvg: number | null;
  cpuMax: number | null;
  memoryAvailableMb: number | null;
  memoryUsedPercent: number | null;
  diskCriticalUsedPct: number | null;
  diskCriticalVolume: string | null;
  watchUnhealthy: number | null;
  watchHealthy: number | null;
  eventLogErrors: number;
  eventLogWarnings: number;
  eventLogTotal: number;
}

export interface HostAnalyticsChannelCount {
  channel: string;
  count: number;
}

export interface HostAnalyticsLevelCount {
  level: string;
  count: number;
}

export type HostSessionHistoryKind =
  | 'logon'
  | 'logoff'
  | 'failed'
  | 'rdp_logon'
  | 'rdp_logoff'
  | 'rdp_disconnect'
  | 'rdp_reconnect'
  | 'ssh_logon'
  | 'ssh_failed'
  | 'sudo'
  | 'other';

export interface HostSessionHistoryItem {
  id: string;
  at: number;
  timestamp: string;
  eventId: string;
  kind: HostSessionHistoryKind;
  user: string | null;
  /** Subject account when different from target (e.g. SYSTEM) */
  subjectUser?: string | null;
  logonType?: string | null;
  /** RDP client address when available (Event 21/24/25). */
  sourceAddress?: string | null;
  preview: string | null;
}

export interface HostAnalyticsBundle {
  range: HostAnalyticsRange;
  metrics: DiscoveryHostMetricsSnapshot;
  apps: DiscoveryHostAppsSnapshot;
  activity: DiscoveryWatchActivitySnapshot;
  eventLogs: DiscoveryHostEventLogSnapshot;
  kpis: HostAnalyticsKpis;
  roles: HostRoleChip[];
  channelCounts: HostAnalyticsChannelCount[];
  levelCounts: HostAnalyticsLevelCount[];
  /** All Event Log items in range (table + pie filter source). */
  eventLogItems: DiscoveryHostEventLogItem[];
  /** @deprecated prefer eventLogItems — kept as alias of first page for older callers */
  recentEvents: DiscoveryHostEventLogItem[];
  sessionHistory: HostSessionHistoryItem[];
}

const PRESET_HOURS: Record<Exclude<HostAnalyticsTimeRange, 'custom'>, number> = {
  '1h': 1,
  '6h': 6,
  '24h': 24,
  '7d': 168,
};

export function parseHostAnalyticsTimeRange(raw: unknown): HostAnalyticsTimeRange {
  const s = typeof raw === 'string' ? raw.trim().toLowerCase() : '';
  if (s === '1h' || s === '6h' || s === '24h' || s === '7d' || s === 'custom') return s;
  return '24h';
}

export function resolveHostAnalyticsRange(opts: {
  timeRange?: HostAnalyticsTimeRange | string | null;
  from?: string | null;
  to?: string | null;
  nowMs?: number;
}): HostAnalyticsRange {
  const now = opts.nowMs ?? Date.now();
  let timeRange = parseHostAnalyticsTimeRange(opts.timeRange);

  if (opts.from && timeRange === 'custom') {
    const fromMs = Date.parse(opts.from);
    const toMs = opts.to ? Date.parse(opts.to) : now;
    if (Number.isFinite(fromMs) && Number.isFinite(toMs) && fromMs < toMs) {
      return {
        timeRange: 'custom',
        from: new Date(fromMs).toISOString(),
        to: new Date(toMs).toISOString(),
        fromMs,
        toMs,
      };
    }
    timeRange = '24h';
  }

  if (opts.from && opts.to && !opts.timeRange) {
    const fromMs = Date.parse(opts.from);
    const toMs = Date.parse(opts.to);
    if (Number.isFinite(fromMs) && Number.isFinite(toMs) && fromMs < toMs) {
      return {
        timeRange: 'custom',
        from: new Date(fromMs).toISOString(),
        to: new Date(toMs).toISOString(),
        fromMs,
        toMs,
      };
    }
  }

  const hours = PRESET_HOURS[timeRange === 'custom' ? '24h' : timeRange];
  const fromMs = now - hours * 60 * 60 * 1000;
  return {
    timeRange: timeRange === 'custom' ? '24h' : timeRange,
    from: new Date(fromMs).toISOString(),
    to: undefined,
    fromMs,
    toMs: now,
  };
}

function seriesStats(points: MetricPoint[]): { last: number | null; avg: number | null; max: number | null } {
  if (!points.length) return { last: null, avg: null, max: null };
  const vals = points.map((p) => p.value).filter((v) => Number.isFinite(v));
  if (!vals.length) return { last: null, avg: null, max: null };
  const last = vals[vals.length - 1]!;
  const max = Math.max(...vals);
  const avg = vals.reduce((a, b) => a + b, 0) / vals.length;
  return {
    last: Math.round(last * 10) / 10,
    avg: Math.round(avg * 10) / 10,
    max: Math.round(max * 10) / 10,
  };
}

function isErrorLevel(level?: string | null): boolean {
  const l = (level || '').toLowerCase();
  return l === 'failure' || l === 'error' || l === 'critical' || l === '2' || l === '1';
}

function isWarningLevel(level?: string | null): boolean {
  const l = (level || '').toLowerCase();
  return l === 'warning' || l === 'unknown' || l === '3';
}

export function detectHostRoles(
  host: Pick<SiemDiscoveryHost, 'hostname' | 'osHint' | 'osFamily' | 'samAccountName'> | null | undefined,
  apps: DiscoveryHostAppsSnapshot,
  eventLogs: DiscoveryHostEventLogSnapshot,
): HostRoleChip[] {
  const roles: HostRoleChip[] = [];
  const blob = [
    host?.hostname,
    host?.osHint,
    host?.samAccountName,
    ...apps.targets.map((t) => `${t.name} ${t.displayName || ''}`),
    ...eventLogs.items.map((e) => `${e.packageName || ''} ${e.channel}`),
  ]
    .join(' ')
    .toLowerCase();

  const isLinux =
    host?.osFamily === 'linux'
    || blob.includes('linux')
    || blob.includes('ubuntu')
    || blob.includes('debian');

  if (
    !isLinux
    && (
      /\bdc\b/.test(blob)
      || blob.includes('domain controller')
      || blob.includes('ad-security')
      || blob.includes('ntds')
      || blob.includes('dfs replication')
    )
  ) {
    roles.push('dc');
  }

  if (
    blob.includes('sql server')
    || blob.includes('mssql')
    || blob.includes('sqlserver')
    || blob.includes('mongod')
    || blob.includes('postgres')
    || /\bsql\b/.test(blob)
  ) {
    roles.push('sql');
  }

  if (!roles.length) {
    if (isLinux) roles.push('memberServer');
    else {
      const os = (host?.osHint || '').toLowerCase();
      if (os.includes('server')) roles.push('memberServer');
      else roles.push('workstation');
    }
  }

  return roles;
}

function buildChannelCounts(items: DiscoveryHostEventLogItem[]): HostAnalyticsChannelCount[] {
  const map = new Map<string, number>();
  for (const row of items) {
    const key = channelFilterKey(row.channel, row.packageName) || row.channel || 'Other';
    map.set(key, (map.get(key) || 0) + 1);
  }
  return [...map.entries()]
    .map(([channel, count]) => ({ channel, count }))
    .sort((a, b) => b.count - a.count);
}

function buildLevelCounts(items: DiscoveryHostEventLogItem[]): HostAnalyticsLevelCount[] {
  const map = new Map<string, number>();
  for (const row of items) {
    const key = (row.level || 'unknown').toLowerCase();
    map.set(key, (map.get(key) || 0) + 1);
  }
  return [...map.entries()]
    .map(([level, count]) => ({ level, count }))
    .sort((a, b) => b.count - a.count);
}

function shortHostKey(hostname: string): string {
  const h = hostname.trim().toLowerCase();
  if (!h) return '';
  if (/^\d{1,3}(\.\d{1,3}){3}$/.test(h)) return h;
  return h.split('.')[0] || h;
}

function matchesHostItem(item: SecEventListItem, hostname: string): boolean {
  const want = shortHostKey(hostname);
  if (!want) return false;
  const candidates = [
    item.sourceHost,
    typeof item.fields?.machine === 'string' ? item.fields.machine : null,
  ];
  for (const raw of candidates) {
    const src = (raw || '').trim().toLowerCase();
    if (!src) continue;
    if (src === want || shortHostKey(src) === want || src.includes(want)) return true;
  }
  return false;
}

function sessionMessageBlob(item: SecEventListItem): string {
  return securityMessageFromEventFields(
    item.fields,
    item.raw,
    item.rawPreview,
    item.eventAction,
  );
}

function sessionKindFromEventId(eventId: string): HostSessionHistoryKind {
  if (eventId === '4624') return 'logon';
  if (eventId === '4634' || eventId === '4647') return 'logoff';
  if (eventId === '4625') return 'failed';
  if (eventId === '21') return 'rdp_logon';
  if (eventId === '23') return 'rdp_logoff';
  if (eventId === '24') return 'rdp_disconnect';
  if (eventId === '25') return 'rdp_reconnect';
  return 'other';
}

function isUsableSessionUser(user: string | null | undefined): boolean {
  if (!user) return false;
  if (isWindowsMachineAccount(user)) return false;
  if (/\\[rtn]/.test(user)) return false;
  if (user.length > 128) return false;
  return true;
}

function extractSessionUser(item: SecEventListItem): {
  user: string | null;
  subjectUser: string | null;
  logonType: string | null;
  sourceAddress: string | null;
} {
  const eventId = String(item.eventCode ?? item.fields?.eventId ?? '').trim();
  const actor = (item.actorUser || '').trim();
  const fields = item.fields ?? {};
  const message = sessionMessageBlob(item);

  if (SESSION_HISTORY_RDP_EVENT_IDS.has(eventId)) {
    const rdp = parseWindowsRdpSessionMessage(message);
    return {
      user: rdp.user || actor || null,
      subjectUser: null,
      logonType: null,
      sourceAddress: rdp.sourceAddress,
    };
  }

  const parsed = parseWindowsSecurityLogonMessage(message);

  if (parsed.displayUser) {
    // Prefer New Logon / target over Subject (often HOST$ / SYSTEM)
    const target = parsed.targetAccount || parsed.displayUser;
    const user = isUsableSessionUser(target)
      ? target
      : (isUsableSessionUser(parsed.displayUser) ? parsed.displayUser : target);
    return {
      user,
      subjectUser: parsed.subjectAccount,
      logonType: parsed.logonType,
      sourceAddress: null,
    };
  }

  for (const key of ['targetUser', 'TargetUserName', 'SubjectUserName', 'AccountName', 'user']) {
    const v = fields[key];
    if (typeof v === 'string' && v.trim()) {
      return {
        user: v.trim(),
        subjectUser: null,
        logonType: parsed.logonType,
        sourceAddress: null,
      };
    }
  }

  return {
    user: actor || null,
    subjectUser: null,
    logonType: parsed.logonType,
    sourceAddress: null,
  };
}

function toSessionHistoryItem(item: SecEventListItem): HostSessionHistoryItem | null {
  const eventId = String(item.eventCode ?? item.fields?.eventId ?? '').trim();
  if (!SESSION_HISTORY_EVENT_IDS.has(eventId)) return null;
  const at = Date.parse(item.timestamp || item.ingestedAt || '');
  if (!Number.isFinite(at)) return null;
  const message = sessionMessageBlob(item);
  const extracted = extractSessionUser(item);
  const previewSource = message || item.rawPreview || item.eventAction || '';
  const preview = previewSource.trim()
    ? previewSource.replace(/\s+/g, ' ').trim().slice(0, 120) + (previewSource.length > 120 ? '…' : '')
    : null;
  return {
    id: item.id || `${at}-${eventId}`,
    at,
    timestamp: item.timestamp || new Date(at).toISOString(),
    eventId,
    kind: sessionKindFromEventId(eventId),
    user: extracted.user,
    subjectUser: extracted.subjectUser,
    logonType: extracted.logonType,
    sourceAddress: extracted.sourceAddress,
    preview,
  };
}

function buildSessionHistoryFromEventLogs(
  items: DiscoveryHostEventLogItem[],
): HostSessionHistoryItem[] {
  const out: HostSessionHistoryItem[] = [];
  for (const row of items) {
    const eventId = (row.eventId || '').trim();
    if (!SESSION_HISTORY_EVENT_IDS.has(eventId)) continue;
    const message = row.message || row.action || '';
    if (SESSION_HISTORY_RDP_EVENT_IDS.has(eventId)) {
      const rdp = parseWindowsRdpSessionMessage(message);
      out.push({
        id: row.id,
        at: row.at,
        timestamp: row.timestamp,
        eventId,
        kind: sessionKindFromEventId(eventId),
        user: rdp.user,
        subjectUser: null,
        logonType: null,
        sourceAddress: rdp.sourceAddress,
        preview: message || row.action,
      });
      continue;
    }
    const parsed = parseWindowsSecurityLogonMessage(message);
    out.push({
      id: row.id,
      at: row.at,
      timestamp: row.timestamp,
      eventId,
      kind: sessionKindFromEventId(eventId),
      user: parsed.targetAccount || parsed.displayUser,
      subjectUser: parsed.subjectAccount,
      logonType: parsed.logonType,
      preview: row.message || row.action,
    });
  }
  return out;
}

function parseSshJournalMessage(message: string): {
  user: string | null;
  sourceAddress: string | null;
} {
  const m =
    /(?:Accepted|Failed) \S+ for(?: invalid user)? (\S+) from (\S+)/i.exec(message)
    || /(?:Accepted|Failed) \S+ for (\S+) from (\S+)/i.exec(message);
  if (!m) return { user: null, sourceAddress: null };
  return { user: m[1] || null, sourceAddress: m[2] || null };
}

function parseSudoJournalUser(message: string): string | null {
  const m = /^(\S+)\s*:/.exec(message.trim()) || /\bsudo:\s+(\S+)\s*:/i.exec(message);
  return m?.[1] || null;
}

/** SSH / sudo rows from linux-journal packages (modal Event Log / Host Analytics). */
export function buildLinuxSessionHistoryFromJournal(
  items: DiscoveryHostEventLogItem[],
): HostSessionHistoryItem[] {
  const out: HostSessionHistoryItem[] = [];
  for (const row of items) {
    const action = (
      row.eventAction
      || row.action
      || row.eventId
      || ''
    ).trim().toLowerCase();
    const pkg = (row.packageName || '').trim().toLowerCase();
    const message = row.message || row.action || '';

    let kind: HostSessionHistoryKind | null = null;
    if (
      action === 'ssh.login_success'
      || action.includes('login_success')
      || (pkg === 'sshd' && /accepted /i.test(message))
    ) {
      kind = 'ssh_logon';
    } else if (
      action === 'ssh.login_failed'
      || action.includes('login_failed')
      || (pkg === 'sshd' && /failed /i.test(message))
    ) {
      kind = 'ssh_failed';
    } else if (action === 'sudo.event' || pkg === 'sudo' || action.startsWith('sudo.')) {
      kind = 'sudo';
    } else if (pkg === 'sshd' || action.startsWith('ssh.')) {
      kind = 'other';
    } else {
      continue;
    }

    const ssh = kind === 'sudo' ? null : parseSshJournalMessage(message);
    const user = kind === 'sudo' ? parseSudoJournalUser(message) : ssh?.user ?? null;
    const previewSource = message || action;
    out.push({
      id: row.id,
      at: row.at,
      timestamp: row.timestamp,
      eventId: row.eventId || action || pkg || 'journal',
      kind,
      user,
      subjectUser: null,
      logonType: null,
      sourceAddress: ssh?.sourceAddress ?? null,
      preview: previewSource.trim()
        ? previewSource.replace(/\s+/g, ' ').trim().slice(0, 120)
          + (previewSource.length > 120 ? '…' : '')
        : null,
    });
  }
  return out;
}

function mergeSessionHistoryItem(
  prev: HostSessionHistoryItem,
  row: HostSessionHistoryItem,
): HostSessionHistoryItem {
  const prevUserOk = isUsableSessionUser(prev.user);
  const rowUserOk = isUsableSessionUser(row.user);
  return {
    ...prev,
    user: (prevUserOk ? prev.user : null) || (rowUserOk ? row.user : null) || prev.user || row.user,
    subjectUser: prev.subjectUser || row.subjectUser,
    logonType: prev.logonType || row.logonType,
    sourceAddress: prev.sourceAddress || row.sourceAddress,
    preview: prev.preview || row.preview,
    // Prefer more specific RDP kind if one side is RDP channel
    kind:
      prev.kind.startsWith('rdp_') || row.kind.startsWith('rdp_')
        ? (prev.kind.startsWith('rdp_') ? prev.kind : row.kind)
        : prev.kind !== 'other'
          ? prev.kind
          : row.kind,
  };
}

async function fetchSessionAuthHistory(
  hostname: string,
  range: HostAnalyticsRange,
): Promise<HostSessionHistoryItem[]> {
  const host = hostname.trim();
  if (!host) return [];
  const want = shortHostKey(host);
  const hostTerm = want || host;
  const base = {
    from: range.from,
    to: range.to,
    sourceType: 'windows-eventlog' as const,
    excludeUnknown: false,
    // API currently caps around 200 per page
    limit: 200,
  };

  // Prod search index: bare "4624" returns empty; host token is required.
  // Event-id tokens are weak — fetch by host and filter IDs client-side.
  // Two pages reduce chance of missing auth/RDP under service/lifecycle noise.
  const results = await Promise.all([
    secEventQuery({ ...base, search: hostTerm, skip: 0 }),
    secEventQuery({ ...base, search: hostTerm, skip: 200 }),
  ]);

  const map = new Map<string, HostSessionHistoryItem>();
  for (const res of results) {
    for (const item of res.items ?? []) {
      if (!matchesHostItem(item, host)) continue;
      const row = toSessionHistoryItem(item);
      if (!row) continue;
      if (row.at < range.fromMs || row.at > range.toMs) continue;
      const prev = map.get(row.id);
      map.set(row.id, prev ? mergeSessionHistoryItem(prev, row) : row);
    }
  }
  return [...map.values()].sort((a, b) => b.at - a.at);
}

function buildKpis(
  host: SiemDiscoveryHost | null | undefined,
  metrics: DiscoveryHostMetricsSnapshot,
  apps: DiscoveryHostAppsSnapshot,
  eventLogs: DiscoveryHostEventLogSnapshot,
  nowMs: number,
): HostAnalyticsKpis {
  const cpu = seriesStats(metrics.cpuSeries);
  const disks = metrics.disks;
  let diskCriticalUsedPct: number | null = null;
  let diskCriticalVolume: string | null = null;
  for (const d of disks) {
    const pct = diskUsedPercent(d);
    if (pct == null) continue;
    if (diskCriticalUsedPct == null || pct > diskCriticalUsedPct) {
      diskCriticalUsedPct = pct;
      diskCriticalVolume = d.volume;
    }
  }
  if (diskCriticalUsedPct == null) {
    const main = primaryDisk(disks);
    if (main) {
      diskCriticalUsedPct = diskUsedPercent(main);
      diskCriticalVolume = main.volume;
    }
  }

  const heartbeatAt = host?.lastSeenAt ?? metrics.freshestAt;
  const heartbeatAgeSec =
    heartbeatAt != null ? Math.max(0, Math.round((nowMs - heartbeatAt) / 1000)) : null;

  const eventLogErrors = eventLogs.items.filter((i) => isErrorLevel(i.level)).length;
  const eventLogWarnings = eventLogs.items.filter((i) => isWarningLevel(i.level)).length;

  return {
    heartbeatAgeSec,
    cpuLast: metrics.cpuPercent ?? cpu.last,
    cpuAvg: cpu.avg,
    cpuMax: cpu.max,
    memoryAvailableMb: metrics.memoryAvailableMb,
    memoryUsedPercent: metrics.memoryUsedPercent,
    diskCriticalUsedPct,
    diskCriticalVolume,
    watchUnhealthy: apps.unhealthyCount,
    watchHealthy: apps.healthyCount,
    eventLogErrors,
    eventLogWarnings,
    eventLogTotal: eventLogs.items.length,
  };
}

function maxPointsForRange(range: HostAnalyticsRange): number {
  const hours = (range.toMs - range.fromMs) / (60 * 60 * 1000);
  if (hours <= 2) return 60;
  if (hours <= 8) return 96;
  if (hours <= 36) return 120;
  return 144;
}

function queryLimitForRange(range: HostAnalyticsRange): number {
  return Math.min(500, Math.max(80, maxPointsForRange(range) * 2));
}

export async function loadHostAnalytics(opts: {
  hostname: string;
  host?: SiemDiscoveryHost | null;
  timeRange?: HostAnalyticsTimeRange | string | null;
  from?: string | null;
  to?: string | null;
}): Promise<HostAnalyticsBundle> {
  const range = resolveHostAnalyticsRange({
    timeRange: opts.timeRange,
    from: opts.from,
    to: opts.to,
  });
  const hostName = opts.hostname.trim();
  const maxPoints = maxPointsForRange(range);
  const limit = queryLimitForRange(range);
  const rangeOpts = { from: range.from, to: range.to };

  const osFamily = opts.host?.osFamily;
  const isLinux = (osFamily || '').toString().trim().toLowerCase() === 'linux';

  const [metrics, apps, activity, eventLogs, sessionHistoryRaw] = await Promise.all([
    fetchDiscoveryHostMetrics(hostName, {
      ...rangeOpts,
      maxPoints,
      limit,
      host: opts.host ?? { hostname: hostName, ip: hostName, agent: null },
    }),
    // Latest defined-target status (inventory) — not limited to the chart range
    fetchDiscoveryHostApps(hostName, {
      host: opts.host ?? { hostname: hostName, ip: hostName, agent: null },
    }),
    fetchDiscoveryHostWatchActivity(hostName, {
      ...rangeOpts,
      limit: 80,
      host: opts.host ?? { hostname: hostName, ip: hostName, agent: null },
    }),
    fetchDiscoveryHostEventLogs(hostName, {
      ...rangeOpts,
      limit: 150,
      osFamily,
      host: opts.host ?? { hostname: hostName, ip: hostName, agent: null },
    }),
    // Windows Security/RDP session history only — Linux journal sessions are L2.
    isLinux ? Promise.resolve([] as HostSessionHistoryItem[]) : fetchSessionAuthHistory(hostName, range),
  ]);

  const kpis = buildKpis(opts.host, metrics, apps, eventLogs, range.toMs);
  const roles = detectHostRoles(opts.host ?? { hostname: hostName }, apps, eventLogs);

  const sessionFromGeneral = isLinux
    ? buildLinuxSessionHistoryFromJournal(eventLogs.items)
    : buildSessionHistoryFromEventLogs(eventLogs.items);
  const sessionMap = new Map<string, HostSessionHistoryItem>();
  for (const row of [...sessionHistoryRaw, ...sessionFromGeneral]) {
    const prev = sessionMap.get(row.id);
    sessionMap.set(row.id, prev ? mergeSessionHistoryItem(prev, row) : row);
  }
  const sessionHistory = [...sessionMap.values()]
    .filter((row) => row.at >= range.fromMs && row.at <= range.toMs)
    .sort((a, b) => b.at - a.at)
    .slice(0, 250);

  return {
    range,
    metrics,
    apps,
    activity,
    eventLogs,
    kpis,
    roles,
    channelCounts: buildChannelCounts(eventLogs.items),
    levelCounts: buildLevelCounts(eventLogs.items),
    eventLogItems: eventLogs.items,
    recentEvents: eventLogs.items.slice(0, 15),
    sessionHistory,
  };
}

export function hostAnalyticsEventsLink(
  hostname: string,
  range: HostAnalyticsRange,
  extra?: {
    sourceType?: string;
    eventAction?: string;
    host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null;
  },
): string {
  const q = new URLSearchParams();
  const term = preferredSecEventSearchTerm(hostname, extra?.host ?? null);
  if (term) q.set('search', term);
  if (extra?.sourceType) q.set('sourceType', extra.sourceType);
  if (extra?.eventAction) q.set('eventAction', extra.eventAction);
  if (range.timeRange === 'custom') {
    q.set('from', range.from);
    if (range.to) q.set('to', range.to);
  } else {
    q.set('timeRange', range.timeRange);
  }
  return `/apps/siem-center/events?${q.toString()}`;
}

/** Human-readable label for the active analytics range (matches dashboard picker). */
export function formatHostAnalyticsRangeLabel(
  range: HostAnalyticsRange,
  locale = 'tr-TR',
): string {
  if (range.timeRange !== 'custom') {
    const presetKeys: Record<string, string> = {
      '1h': '1h',
      '6h': '6h',
      '24h': '24h',
      '7d': '7d',
    };
    return presetKeys[range.timeRange] || range.timeRange;
  }
  try {
    const fmt = new Intl.DateTimeFormat(locale, {
      dateStyle: 'short',
      timeStyle: 'short',
    });
    return `${fmt.format(new Date(range.fromMs))} – ${fmt.format(new Date(range.toMs))}`;
  } catch {
    return `${range.from} – ${range.to || ''}`;
  }
}
