import type { AlarmStatus, AlarmSummary } from '@/types/apps/alarm';
import { SIEM_SCENARIO_CATALOG } from '@/composables/useSiemScenarioCatalog';
import { formatRelativeTime, fromDatetimeLocalInput, isValidCustomRange, toDatetimeLocalInput } from '@/composables/useSecEventList';

export const ALARM_PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;
export const ALARM_DEFAULT_PAGE_SIZE = 25;

export type AlarmListView = 'inbox' | 'history';

export type AlarmStatusFilter = 'open' | 'all' | AlarmStatus;

export type AlarmHistoryRangeMode = 'preset' | 'custom';

export interface AlarmLifecycleHistoryEntry {
  action: string;
  fromStatus?: string;
  toStatus?: string;
  at: string;
  byUserId?: string;
  byUserName?: string;
  source?: string;
  reason?: string;
}

const INTERNAL_CONTEXT_KEYS = new Set([
  'lifecycleHistory',
  'manualAction',
  'manualActionAt',
  'manualActionBy',
  'manualActionByUserId',
]);

export function parseLifecycleHistory(alarm: AlarmSummary): AlarmLifecycleHistoryEntry[] {
  const ctx = alarm.context ?? {};
  const raw = ctx.lifecycleHistory ?? ctx.LifecycleHistory;
  const parsed = parseLifecycleHistoryItems(raw);

  if (parsed.length === 0) {
    const action = ctx.manualAction ?? ctx.ManualAction;
    const at = ctx.manualActionAt ?? ctx.ManualActionAt;
    const by = ctx.manualActionBy ?? ctx.ManualActionBy;
    if (action && at) {
      parsed.push({
        action: String(action),
        at: String(at),
        byUserName: by != null ? String(by) : undefined,
        source: 'manual',
      });
    }
  }

  const hasRaised = parsed.some(
    (entry) => entry.action === 'Active' && (entry.reason === 'alarm_raised' || entry.source === 'automatic'),
  );
  if (!hasRaised && alarm.firstSeenAt) {
    parsed.push({
      action: 'Active',
      at: alarm.firstSeenAt,
      byUserName: 'system',
      source: 'automatic',
      reason: 'alarm_raised',
    });
  }

  return parsed.sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime());
}

function parseLifecycleHistoryItems(raw: unknown): AlarmLifecycleHistoryEntry[] {
  if (raw == null) return [];

  let value: unknown = raw;
  if (typeof value === 'string') {
    try {
      value = JSON.parse(value);
    } catch {
      return [];
    }
  }

  const items = Array.isArray(value)
    ? value
    : typeof value === 'object'
      ? Object.values(value as Record<string, unknown>)
      : [];

  const parsed: AlarmLifecycleHistoryEntry[] = [];
  for (const item of items) {
    if (!item || typeof item !== 'object') continue;
    const row = item as Record<string, unknown>;
    const at = readField(row, 'at', 'At');
    if (!at) continue;
    parsed.push({
      action: readField(row, 'action', 'Action', 'toStatus', 'ToStatus') ?? '',
      fromStatus: readField(row, 'fromStatus', 'FromStatus'),
      toStatus: readField(row, 'toStatus', 'ToStatus'),
      at,
      byUserId: readField(row, 'byUserId', 'ByUserId'),
      byUserName: readField(row, 'byUserName', 'ByUserName'),
      source: readField(row, 'source', 'Source'),
      reason: readField(row, 'reason', 'Reason'),
    });
  }

  return parsed;
}

function readField(row: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const value = row[key];
    if (value == null || value === '') continue;
    return String(value);
  }
  return undefined;
}

export function lifecycleActionLabel(
  action: string,
  t: (key: string) => string,
): string {
  switch (action) {
    case 'Acknowledged':
      return t('alarmCenter.alarms.statusAcknowledged');
    case 'Suppressed':
      return t('alarmCenter.alarms.statusSuppressed');
    case 'Resolved':
      return t('alarmCenter.alarms.statusResolved');
    case 'Active':
      return t('alarmCenter.alarms.statusActive');
    default:
      return action;
  }
}

export function computeHistoryRange(days: number): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to.getTime() - days * 86400_000);
  return { from: from.toISOString(), to: to.toISOString() };
}

export function initAlarmCustomRangeFromDays(days: number): { fromLocal: string; toLocal: string } {
  const range = computeHistoryRange(days);
  return {
    fromLocal: toDatetimeLocalInput(range.from),
    toLocal: toDatetimeLocalInput(range.to),
  };
}

export function buildAlarmHistoryRange(
  mode: AlarmHistoryRangeMode,
  days: number,
  customFromLocal: string,
  customToLocal: string,
): { from: string; to: string } | null {
  if (mode === 'custom') {
    const from = fromDatetimeLocalInput(customFromLocal);
    const to = fromDatetimeLocalInput(customToLocal) ?? new Date().toISOString();
    if (!from || !isValidCustomRange(from, to)) return null;
    return { from, to };
  }
  return computeHistoryRange(days);
}

export function resolveAlarmListStatus(
  statusFilter: AlarmStatusFilter,
  isHistory: boolean,
): { openOnly: boolean; status?: AlarmStatus } {
  if (statusFilter === 'open') {
    return { openOnly: !isHistory, status: undefined };
  }
  if (statusFilter === 'all') {
    return { openOnly: false, status: undefined };
  }
  return { openOnly: false, status: statusFilter };
}

export interface AlarmListStats {
  shown: number;
  total: number;
  pageFrom: number;
  pageTo: number;
  highSeverity: number;
  activeCount: number;
}

export interface AlarmContextField {
  key: string;
  labelKey: string;
  value: string;
}

const CONTEXT_FIELD_DEFS: { key: string; labelKey: string }[] = [
  { key: 'key', labelKey: 'alarmCenter.alarms.ctxMatchKey' },
  { key: 'userId', labelKey: 'alarmCenter.alarms.ctxUserId' },
  { key: 'srcIp', labelKey: 'alarmCenter.alarms.ctxSrcIp' },
  { key: 'dstIp', labelKey: 'alarmCenter.alarms.ctxDstIp' },
  { key: 'windowCount', labelKey: 'alarmCenter.alarms.ctxWindowCount' },
  { key: 'value', labelKey: 'alarmCenter.alarms.ctxValue' },
];

export function contextMatchKey(alarm: AlarmSummary): string | null {
  const key = alarm.context?.key;
  return typeof key === 'string' && key.trim() ? key.trim() : null;
}

export function getScenarioIdForAlarm(alarm: AlarmSummary): string | null {
  const matchKey = contextMatchKey(alarm);
  if (!matchKey) return null;
  const def = SIEM_SCENARIO_CATALOG.find((s) => s.matchKey === matchKey);
  return def?.id ?? null;
}

export function formatAlarmScenarioLabel(
  alarm: AlarmSummary,
  t: (key: string) => string,
): string {
  const matchKey = contextMatchKey(alarm);
  if (!matchKey) return '—';
  const def = SIEM_SCENARIO_CATALOG.find((s) => s.matchKey === matchKey);
  if (def) {
    const titleKey = `siemCenter.scenarios.${def.id}.title`;
    const title = t(titleKey);
    return title !== titleKey ? `${def.id} · ${title}` : `${def.id} · ${matchKey}`;
  }
  return matchKey;
}

export function formatAlarmSummary(alarm: AlarmSummary): string {
  const ctx = alarm.context ?? {};
  const parts: string[] = [];
  for (const field of ['userId', 'srcIp', 'dstIp', 'windowCount', 'value'] as const) {
    const val = ctx[field];
    if (val != null && String(val).trim()) parts.push(String(val));
  }
  if (parts.length > 0) return parts.join(' · ');
  const dk = alarm.dedupKey ?? '';
  return dk.length > 72 ? `${dk.slice(0, 72)}…` : dk || '—';
}

export function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

export function statusColor(status: AlarmSummary['status']): string {
  if (status === 'Active' || status === 0) return 'error';
  if (status === 'Acknowledged' || status === 1) return 'warning';
  if (status === 'Resolved' || status === 2) return 'success';
  if (status === 'Suppressed' || status === 3) return 'default';
  return 'default';
}

export function statusLabel(
  status: AlarmSummary['status'],
  t: (key: string) => string,
): string {
  if (status === 'Active' || status === 0) return t('alarmCenter.alarms.statusActive');
  if (status === 'Acknowledged' || status === 1) return t('alarmCenter.alarms.statusAcknowledged');
  if (status === 'Resolved' || status === 2) return t('alarmCenter.alarms.statusResolved');
  if (status === 'Suppressed' || status === 3) return t('alarmCenter.alarms.statusSuppressed');
  return String(status);
}

export function computeAlarmListStats(
  items: AlarmSummary[],
  total: number,
  skip = 0,
): AlarmListStats {
  let highSeverity = 0;
  let activeCount = 0;

  for (const item of items) {
    if (item.severity >= 8) highSeverity++;
    if (item.status === 'Active' || item.status === 0) activeCount++;
  }

  const pageFrom = total === 0 ? 0 : skip + 1;
  const pageTo = total === 0 ? 0 : Math.min(skip + items.length, total);

  return {
    shown: items.length,
    total,
    pageFrom,
    pageTo,
    highSeverity,
    activeCount,
  };
}

export function extractContextFields(alarm: AlarmSummary): AlarmContextField[] {
  const ctx = alarm.context ?? {};
  const fields: AlarmContextField[] = [];

  for (const def of CONTEXT_FIELD_DEFS) {
    const raw = ctx[def.key];
    if (raw == null || !String(raw).trim()) continue;
    fields.push({ key: def.key, labelKey: def.labelKey, value: String(raw) });
  }

  for (const [key, raw] of Object.entries(ctx)) {
    if (INTERNAL_CONTEXT_KEYS.has(key)) continue;
    if (CONTEXT_FIELD_DEFS.some((d) => d.key === key)) continue;
    if (raw == null || !String(raw).trim()) continue;
    fields.push({ key, labelKey: key, value: String(raw) });
  }

  return fields;
}

export function eventsLinkForAlarm(alarm: AlarmSummary): string | null {
  const matchKey = contextMatchKey(alarm);
  if (matchKey) return `/apps/siem-center/events?eventAction=${encodeURIComponent(matchKey)}`;
  const userId = alarm.context?.userId;
  if (typeof userId === 'string' && userId.trim()) {
    return `/apps/siem-center/events?search=${encodeURIComponent(userId.trim())}`;
  }
  return null;
}

export function ruleLinkForAlarm(alarm: AlarmSummary): string {
  return `/apps/alarm-center/rules?ruleId=${encodeURIComponent(alarm.ruleId)}`;
}

export function buildRelatedEventsQuery(alarm: AlarmSummary): {
  from: string;
  to?: string;
  eventAction?: string;
  search?: string;
  srcIp?: string;
  actorUser?: string;
  limit: number;
} {
  const firstMs = new Date(alarm.firstSeenAt).getTime();
  const lastMs = new Date(alarm.lastSeenAt).getTime();
  const padMs = 15 * 60_000;
  const from = new Date(Math.min(firstMs, lastMs) - padMs).toISOString();
  const to = new Date(Math.max(firstMs, lastMs) + padMs).toISOString();

  const matchKey = contextMatchKey(alarm);
  const ctx = alarm.context ?? {};
  const query: ReturnType<typeof buildRelatedEventsQuery> = { from, to, limit: 10 };

  if (matchKey) query.eventAction = matchKey;

  const userId = ctx.userId;
  if (typeof userId === 'string' && userId.trim()) query.actorUser = userId.trim();

  const srcIp = ctx.srcIp;
  if (typeof srcIp === 'string' && srcIp.trim()) query.srcIp = srcIp.trim();

  if (!matchKey && !query.actorUser && !query.srcIp) {
    const searchParts = [ctx.userId, ctx.srcIp, ctx.dstIp].filter((v) => v != null && String(v).trim());
    if (searchParts.length > 0) query.search = String(searchParts[0]);
  }

  return query;
}

export function isAlarmActionable(status: AlarmSummary['status']): boolean {
  return status === 'Active' || status === 0 || status === 'Acknowledged' || status === 1;
}

export function formatAlarmRelativeTime(
  value: string | undefined | null,
  locale: string,
  t: (key: string, params?: Record<string, unknown>) => string,
): string {
  return formatRelativeTime(value, locale, t);
}

export { formatRelativeTime, copyTextToClipboard } from '@/composables/useSecEventList';
