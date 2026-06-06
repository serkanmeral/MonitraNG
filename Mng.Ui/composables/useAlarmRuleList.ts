import type { AlarmRule } from '@/types/apps/alarm';
import { operatorSymbol } from '@/composables/useAlarmRuleFormCatalog';
import { SIEM_SCENARIO_CATALOG } from '@/composables/useSiemScenarioCatalog';

export type AlarmRuleSourceKind = 'siem-pack' | 'metric' | 'manual' | 'test';

export type AlarmRuleListViewMode = 'table' | 'siem';

export interface AlarmRuleListStats {
  total: number;
  enabled: number;
  disabled: number;
  siemPack: number;
  metric: number;
  manual: number;
}

export interface AlarmRuleListFilters {
  search: string;
  type: string;
  source: string;
  minSeverity: number | null;
  enabledOnly: boolean;
}

export interface SiemScenarioRuleRow {
  scenarioId: string;
  matchKey: string;
  rule: AlarmRule | null;
}

const METRIC_MATCH_KEYS = new Set(['cpu_usage', 'mem_usage', 'disk_free_percent']);

const E2E_NAME_PATTERN = /\bE2E\b/i;

export function classifyRuleSource(rule: AlarmRule): AlarmRuleSourceKind {
  if (rule.metadata?.packageId) return 'siem-pack';
  if (E2E_NAME_PATTERN.test(rule.name)) return 'test';
  if (rule.type === 'threshold' && METRIC_MATCH_KEYS.has(rule.matchKey)) return 'metric';
  if (rule.type === 'threshold') return 'metric';
  return 'manual';
}

export function getRuleScenarioId(rule: AlarmRule): string | null {
  if (rule.metadata?.scenarioId) return rule.metadata.scenarioId;
  const hit = SIEM_SCENARIO_CATALOG.find((s) => s.matchKey === rule.matchKey);
  return hit?.id ?? null;
}

export function buildRuleConditionSummary(
  rule: AlarmRule,
  t: (key: string, params?: Record<string, unknown>) => string,
): string {
  const groupBy =
    rule.groupByFields?.length > 0
      ? rule.groupByFields.join(' + ')
      : t('alarmCenter.rules.previewAllEvents');

  if (rule.type === 'threshold') {
    return t('alarmCenter.rules.listConditionThreshold', {
      matchKey: rule.matchKey,
      operator: operatorSymbol(rule.operator),
      threshold: String(rule.threshold),
    });
  }
  if (rule.type === 'correlation') {
    return t('alarmCenter.rules.listConditionCorrelation', {
      window: String(rule.windowMinutes),
      matchKey: rule.matchKey,
      groupBy,
      threshold: String(rule.threshold),
    });
  }
  if (rule.type === 'scheduled') {
    return t('alarmCenter.rules.listConditionScheduled', {
      matchKey: rule.matchKey,
      staleness: String(rule.stalenessMinutes),
    });
  }
  if (rule.type === 'sequence') {
    const steps = rule.sequenceSteps ?? [];
    if (steps.length >= 2) {
      return t('alarmCenter.rules.listConditionSequenceDetail', {
        matchKey: rule.matchKey,
        step0Key: steps[0].matchKey,
        step0Count: String(steps[0].minCount ?? 1),
        step1Key: steps[1].matchKey,
      });
    }
    return t('alarmCenter.rules.listConditionSequence', { matchKey: rule.matchKey });
  }
  return t('alarmCenter.rules.listConditionUnknown');
}

export function computeRuleListStats(rules: AlarmRule[]): AlarmRuleListStats {
  let enabled = 0;
  let siemPack = 0;
  let metric = 0;
  let manual = 0;

  for (const rule of rules) {
    if (rule.enabled) enabled++;
    const source = classifyRuleSource(rule);
    if (source === 'siem-pack') siemPack++;
    else if (source === 'metric') metric++;
    else manual++;
  }

  return {
    total: rules.length,
    enabled,
    disabled: rules.length - enabled,
    siemPack,
    metric,
    manual,
  };
}

export function buildDuplicateMatchKeySet(rules: AlarmRule[]): Set<string> {
  const counts = new Map<string, number>();
  for (const rule of rules) {
    const key = `${rule.type}|${rule.matchKey}`;
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  const dupes = new Set<string>();
  for (const [key, count] of counts) {
    if (count > 1) dupes.add(key);
  }
  return dupes;
}

export function isDuplicateRule(rule: AlarmRule, duplicateKeys: Set<string>): boolean {
  return duplicateKeys.has(`${rule.type}|${rule.matchKey}`);
}

export function filterAlarmRules(rules: AlarmRule[], filters: AlarmRuleListFilters): AlarmRule[] {
  const q = filters.search.trim().toLowerCase();
  return rules.filter((rule) => {
    if (filters.enabledOnly && !rule.enabled) return false;
    if (filters.type && rule.type !== filters.type) return false;
    if (filters.source && classifyRuleSource(rule) !== filters.source) return false;
    if (filters.minSeverity != null && rule.severity < filters.minSeverity) return false;
    if (!q) return true;
    const scenario = getRuleScenarioId(rule) ?? '';
    return (
      rule.name.toLowerCase().includes(q) ||
      rule.matchKey.toLowerCase().includes(q) ||
      scenario.toLowerCase().includes(q)
    );
  });
}

export function buildSiemScenarioRows(rules: AlarmRule[]): SiemScenarioRuleRow[] {
  const byScenario = new Map<string, AlarmRule>();
  const byMatchKey = new Map<string, AlarmRule>();

  for (const rule of rules) {
    const scenarioId = getRuleScenarioId(rule);
    if (scenarioId && !byScenario.has(scenarioId)) byScenario.set(scenarioId, rule);
    if (!byMatchKey.has(rule.matchKey)) byMatchKey.set(rule.matchKey, rule);
  }

  return SIEM_SCENARIO_CATALOG.map((def) => ({
    scenarioId: def.id,
    matchKey: def.matchKey,
    rule: byScenario.get(def.id) ?? byMatchKey.get(def.matchKey) ?? null,
  }));
}

export function ruleSourceLabelKey(source: AlarmRuleSourceKind): string {
  switch (source) {
    case 'siem-pack':
      return 'alarmCenter.rules.sourceSiemPack';
    case 'metric':
      return 'alarmCenter.rules.sourceMetric';
    case 'test':
      return 'alarmCenter.rules.sourceTest';
    default:
      return 'alarmCenter.rules.sourceManual';
  }
}

export function ruleSourceColor(source: AlarmRuleSourceKind): string {
  switch (source) {
    case 'siem-pack':
      return 'primary';
    case 'metric':
      return 'teal';
    case 'test':
      return 'warning';
    default:
      return 'default';
  }
}

export const RULE_LIST_VIEW_STORAGE_KEY = 'alarm-rules-list-view-v1';

export function loadRuleListViewMode(): AlarmRuleListViewMode {
  if (import.meta.server) return 'table';
  try {
    const raw = localStorage.getItem(RULE_LIST_VIEW_STORAGE_KEY);
    return raw === 'siem' ? 'siem' : 'table';
  } catch {
    return 'table';
  }
}

export function saveRuleListViewMode(mode: AlarmRuleListViewMode): void {
  if (import.meta.server) return;
  localStorage.setItem(RULE_LIST_VIEW_STORAGE_KEY, mode);
}
