import type { SecEventListItem, SecEventRangeMode, SecEventTimeRange } from '@/types/apps/secEvent';
import { SIEM_SCENARIO_CATALOG } from '@/composables/useSiemScenarioCatalog';

export interface SecEventActionOption {
  value: string;
  labelKey: string;
  scenarioId?: string;
}

export const SEC_EVENT_ACTION_OPTIONS: SecEventActionOption[] = [
  { value: 'login_failed', labelKey: 'siemCenter.events.actions.login_failed', scenarioId: 'U1' },
  { value: 'login_success', labelKey: 'siemCenter.events.actions.login_success' },
  { value: 'login_success_after_failures', labelKey: 'siemCenter.events.actions.login_success_after_failures', scenarioId: 'U2' },
  { value: 'privileged_login_outside_window', labelKey: 'siemCenter.events.actions.privileged_login_outside_window', scenarioId: 'U3' },
  { value: 'denied_flow', labelKey: 'siemCenter.events.actions.denied_flow', scenarioId: 'U4' },
  { value: 'allowed_flow', labelKey: 'siemCenter.events.actions.allowed_flow', scenarioId: 'U5' },
  { value: 'rule_change', labelKey: 'siemCenter.events.actions.rule_change', scenarioId: 'U6' },
  { value: 'new_flow', labelKey: 'siemCenter.events.actions.new_flow', scenarioId: 'U7' },
  { value: 'group_member_added', labelKey: 'siemCenter.events.actions.group_member_added', scenarioId: 'U8' },
  { value: 'account_created', labelKey: 'siemCenter.events.actions.account_created', scenarioId: 'U9' },
  { value: 'directory_object_modified', labelKey: 'siemCenter.events.actions.directory_object_modified', scenarioId: 'U10' },
  { value: 'privilege_denied', labelKey: 'siemCenter.events.actions.privilege_denied' },
  { value: 'host.up', labelKey: 'siemCenter.events.actions.host_up' },
  { value: 'watch.inventory', labelKey: 'siemCenter.events.actions.watch_inventory' },
  { value: 'unknown', labelKey: 'siemCenter.events.actions.unknown' },
];

export interface SecEventFilterPreset {
  key: string;
  scenarioId: string;
  eventAction: string;
}

export const SEC_EVENT_FILTER_PRESETS: SecEventFilterPreset[] = [
  { key: 'u1', scenarioId: 'U1', eventAction: 'login_failed' },
  { key: 'u2', scenarioId: 'U2', eventAction: 'login_success_after_failures' },
  { key: 'u3', scenarioId: 'U3', eventAction: 'privileged_login_outside_window' },
  { key: 'u4', scenarioId: 'U4', eventAction: 'denied_flow' },
  { key: 'u5', scenarioId: 'U5', eventAction: 'allowed_flow' },
  { key: 'u6', scenarioId: 'U6', eventAction: 'rule_change' },
  { key: 'u7', scenarioId: 'U7', eventAction: 'new_flow' },
  { key: 'u8', scenarioId: 'U8', eventAction: 'group_member_added' },
  { key: 'u9', scenarioId: 'U9', eventAction: 'account_created' },
  { key: 'u10', scenarioId: 'U10', eventAction: 'directory_object_modified' },
];

export interface SecEventListStats {
  shown: number;
  total: number;
  pageFrom: number;
  pageTo: number;
  failureLike: number;
  sourceTypes: number;
}

export interface SecEventQueryRange {
  from: string;
  to?: string;
}

export const SEC_EVENT_PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;
export const SEC_EVENT_DEFAULT_PAGE_SIZE = 25;

export function getScenarioIdForAction(eventAction: string): string | null {
  const fromCatalog = SIEM_SCENARIO_CATALOG.find((s) => s.matchKey === eventAction);
  if (fromCatalog) return fromCatalog.id;
  const fromOpt = SEC_EVENT_ACTION_OPTIONS.find((o) => o.value === eventAction);
  return fromOpt?.scenarioId ?? null;
}

export function actionColor(action: string): string {
  if (action.includes('fail') || action.includes('denied') || action === 'unknown') return 'error';
  if (action.includes('success') || action === 'host.up') return 'success';
  if (action.includes('new_flow') || action.includes('privileged') || action.includes('rule_change') || action === 'watch.inventory') return 'warning';
  return 'info';
}

export function outcomeColor(outcome?: string | null): string {
  if (!outcome) return 'default';
  const o = outcome.toLowerCase();
  if (o === 'failure' || o === 'deny' || o === 'denied') return 'error';
  if (o === 'success') return 'success';
  return 'info';
}

export function sourceTypeLabelKey(sourceType?: string | null): string {
  switch (sourceType) {
    case 'firewall':
      return 'siemCenter.events.sourceFirewall';
    case 'ad':
      return 'siemCenter.events.sourceAd';
    case 'endpoint':
      return 'siemCenter.events.sourceEndpoint';
    case 'metric':
      return 'siemCenter.events.sourceMetric';
    case 'windows-eventlog':
      return 'siemCenter.events.sourceWindowsEventLog';
    case 'linux-journal':
      return 'siemCenter.events.sourceLinuxJournal';
    default:
      return 'siemCenter.events.sourceUnknown';
  }
}

export function computeSecEventListStats(
  items: SecEventListItem[],
  total: number,
  skip = 0,
): SecEventListStats {
  const sources = new Set<string>();
  let failureLike = 0;

  for (const item of items) {
    if (item.sourceType) sources.add(item.sourceType);
    const outcome = item.eventOutcome?.toLowerCase() ?? '';
    if (
      outcome === 'failure' ||
      outcome === 'deny' ||
      outcome === 'denied' ||
      item.eventAction.includes('fail') ||
      item.eventAction.includes('denied')
    ) {
      failureLike++;
    }
  }

  const pageFrom = total === 0 ? 0 : skip + 1;
  const pageTo = total === 0 ? 0 : Math.min(skip + items.length, total);

  return {
    shown: items.length,
    total,
    pageFrom,
    pageTo,
    failureLike,
    sourceTypes: sources.size,
  };
}

export function computePresetRangeFrom(range: SecEventTimeRange): string {
  const now = Date.now();
  const hours = range === '1h' ? 1 : range === '7d' ? 168 : 24;
  return new Date(now - hours * 3600_000).toISOString();
}

export function buildSecEventQueryRange(
  mode: SecEventRangeMode,
  preset: SecEventTimeRange,
  customFrom?: string | null,
  customTo?: string | null,
): SecEventQueryRange {
  if (mode === 'custom' && customFrom) {
    const fromDate = new Date(customFrom);
    const toDate = customTo ? new Date(customTo) : new Date();
    return {
      from: fromDate.toISOString(),
      to: toDate.toISOString(),
    };
  }
  return { from: computePresetRangeFrom(preset) };
}

export function toDatetimeLocalInput(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function fromDatetimeLocalInput(value: string): string | null {
  if (!value) return null;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  return d.toISOString();
}

export function isValidCustomRange(fromIso: string, toIso: string): boolean {
  const from = new Date(fromIso).getTime();
  const to = new Date(toIso).getTime();
  return Number.isFinite(from) && Number.isFinite(to) && to >= from;
}

export function formatActiveRangeLabel(
  mode: SecEventRangeMode,
  preset: SecEventTimeRange,
  customFrom: string | null,
  customTo: string | null,
  locale: string,
  t: (key: string) => string,
): string {
  if (mode === 'custom' && customFrom && customTo) {
    try {
      const fmt = new Intl.DateTimeFormat(locale === 'tr' ? 'tr-TR' : 'en-GB', {
        dateStyle: 'short',
        timeStyle: 'short',
      });
      return `${fmt.format(new Date(customFrom))} – ${fmt.format(new Date(customTo))}`;
    } catch {
      return t('siemCenter.events.rangeCustom');
    }
  }
  return t(timeRangeLabelKey(preset));
}

export function formatRelativeTime(
  value: string | undefined | null,
  locale: string,
  t: (key: string, params?: Record<string, unknown>) => string,
): string {
  if (!value) return '—';
  try {
    const then = new Date(value).getTime();
    const diffSec = Math.round((Date.now() - then) / 1000);
    if (diffSec < 60) return t('siemCenter.events.relativeJustNow');
    if (diffSec < 3600) return t('siemCenter.events.relativeMinutes', { n: Math.floor(diffSec / 60) });
    if (diffSec < 86400) return t('siemCenter.events.relativeHours', { n: Math.floor(diffSec / 3600) });
    return t('siemCenter.events.relativeDays', { n: Math.floor(diffSec / 86400) });
  } catch {
    return '—';
  }
}

export function timeRangeLabelKey(range: SecEventTimeRange): string {
  switch (range) {
    case '1h':
      return 'siemCenter.events.range1h';
    case '7d':
      return 'siemCenter.events.range7d';
    default:
      return 'siemCenter.events.range24h';
  }
}

export function displayEventAction(item: SecEventListItem): string {
  if (item.baselineNewFlowPair) return `${item.eventAction} + new_flow`;
  return item.eventAction;
}

export function formatRawForDisplay(raw: string): { text: string; isJson: boolean } {
  const trimmed = raw.trim();
  if (!trimmed) return { text: '', isJson: false };
  try {
    if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
      return { text: JSON.stringify(JSON.parse(trimmed), null, 2), isJson: true };
    }
  } catch {
    /* plain text */
  }
  return { text: raw, isJson: false };
}

export function alarmRulesLinkForAction(eventAction: string): string {
  return `/apps/alarm-center/rules?search=${encodeURIComponent(eventAction)}`;
}
