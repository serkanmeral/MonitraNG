import type { ScenarioDedup, ScenarioNodeConfig } from '@/types/apps/scenario';

/** Common observation dimension keys offered in the Alarm node grouping picker. */
export const ALARM_GROUP_BY_FIELD_OPTIONS = [
  { value: 'sourceHost', labelKey: 'alarmCenter.rules.groupByFields.sourceHost' },
  { value: 'userId', labelKey: 'alarmCenter.rules.groupByFields.userId' },
  { value: 'srcIp', labelKey: 'alarmCenter.rules.groupByFields.srcIp' },
  { value: 'dstIp', labelKey: 'alarmCenter.rules.groupByFields.dstIp' },
  { value: 'dstPort', labelKey: 'alarmCenter.rules.groupByFields.dstPort' },
  { value: 'sourceType', labelKey: 'alarmCenter.rules.groupByFields.sourceType' },
] as const;

export type AlarmMergeScope = 'none' | 'all' | 'host' | 'user' | 'hostUser' | 'custom';

export function defaultAlarmDedup(): ScenarioDedup {
  return {
    keyTemplate: '{scenarioId}:{outputNodeId}',
    cooldownSeconds: 300,
    mergeEnabled: true,
  };
}

export function normalizeAlarmDedup(dedup?: Partial<ScenarioDedup> | null): ScenarioDedup {
  const base = defaultAlarmDedup();
  return {
    keyTemplate: String(dedup?.keyTemplate?.trim() || base.keyTemplate),
    cooldownSeconds: Math.max(0, Number(dedup?.cooldownSeconds ?? base.cooldownSeconds) || 0),
    mergeEnabled: dedup?.mergeEnabled !== false,
  };
}

/** Derive dedup key template from grouping (simple mode). */
export function buildAlarmDedupTemplate(groupBy: string[]): string {
  if (!groupBy.length) return '{scenarioId}:{outputNodeId}';
  return '{scenarioId}:{outputNodeId}:{groupKey}';
}

export function inferMergeScope(mergeEnabled: boolean, groupBy: string[]): AlarmMergeScope {
  if (!mergeEnabled) return 'none';
  const fields = groupBy.map(f => f.trim()).filter(Boolean);
  if (fields.length === 0) return 'all';
  if (fields.length === 1 && fields[0] === 'sourceHost') return 'host';
  if (fields.length === 1 && fields[0] === 'userId') return 'user';
  if (fields.length === 2
    && fields.includes('sourceHost')
    && fields.includes('userId')) {
    return 'hostUser';
  }
  return 'custom';
}

export function groupByForMergeScope(scope: AlarmMergeScope, customFields: string[] = []): string[] {
  switch (scope) {
    case 'none':
    case 'all':
      return [];
    case 'host':
      return ['sourceHost'];
    case 'user':
      return ['userId'];
    case 'hostUser':
      return ['sourceHost', 'userId'];
    case 'custom':
      return customFields.map(f => f.trim()).filter(Boolean);
    default:
      return [];
  }
}

export function applyMergeScopeToConfig(
  config: ScenarioNodeConfig,
  scope: AlarmMergeScope,
  customFields?: string[],
): ScenarioNodeConfig {
  const dedup = normalizeAlarmDedup(config.dedup);
  const mergeEnabled = scope !== 'none';
  const groupBy = groupByForMergeScope(scope, customFields ?? config.groupBy ?? []);
  return {
    ...config,
    groupBy,
    dedup: {
      ...dedup,
      mergeEnabled,
      keyTemplate: buildAlarmDedupTemplate(groupBy),
      cooldownSeconds: mergeEnabled ? (dedup.cooldownSeconds || 300) : 0,
    },
  };
}

export function alarmOutputSubtitle(config: ScenarioNodeConfig): string {
  const dedup = normalizeAlarmDedup(config.dedup);
  const severity = config.severity ?? 5;
  if (!dedup.mergeEnabled) {
    return `severity ${severity} · new each time`;
  }
  const group = (config.groupBy ?? []).filter(Boolean);
  const groupLabel = group.length ? group.join('+') : 'all';
  const cooldownMin = Math.round((dedup.cooldownSeconds || 0) / 60);
  return `severity ${severity} · merge · ${groupLabel} · ${cooldownMin}m`;
}
