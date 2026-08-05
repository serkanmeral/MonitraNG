import type { ScenarioSource } from '@/types/apps/scenario';

/** User-facing source families shown in the flow editor. */
export type ScenarioSourceFamily = 'eventlog' | 'metric' | 'app-service';

export type AppServiceMode = 'live' | 'staleness';

export interface ScenarioSourcePreset {
  value: string;
  labelKey: string;
}

export const EVENTLOG_MATCH_PRESETS: ScenarioSourcePreset[] = [
  { value: 'rdp.logon', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.rdp_logon' },
  { value: 'rdp.logoff', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.rdp_logoff' },
  { value: 'rdp.disconnect', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.rdp_disconnect' },
  { value: 'rdp.reconnect', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.rdp_reconnect' },
  { value: 'login_failed', labelKey: 'alarmCenter.rules.matchKeys.login_failed' },
  { value: 'login_success', labelKey: 'alarmCenter.rules.matchKeys.login_success' },
  { value: 'denied_flow', labelKey: 'alarmCenter.rules.matchKeys.denied_flow' },
  { value: 'allowed_flow', labelKey: 'alarmCenter.rules.matchKeys.allowed_flow' },
  { value: 'rule_change', labelKey: 'alarmCenter.rules.matchKeys.rule_change' },
  { value: 'group_member_added', labelKey: 'alarmCenter.rules.matchKeys.group_member_added' },
  { value: 'account_created', labelKey: 'alarmCenter.rules.matchKeys.account_created' },
  { value: 'directory_object_modified', labelKey: 'alarmCenter.rules.matchKeys.directory_object_modified' },
];

export const METRIC_MATCH_PRESETS: ScenarioSourcePreset[] = [
  { value: 'cpu_usage', labelKey: 'alarmCenter.rules.matchKeys.cpu_usage' },
  { value: 'disk_free_percent', labelKey: 'alarmCenter.rules.matchKeys.disk_free_percent' },
  { value: 'memory_usage', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.memory_usage' },
  { value: 'agent_heartbeat', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.agent_heartbeat' },
];

export const APP_SERVICE_MATCH_PRESETS: ScenarioSourcePreset[] = [
  { value: 'service_health', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.service_health' },
  { value: 'app.error', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.app_error' },
  { value: 'agent_heartbeat', labelKey: 'alarmCenter.scenarioStudio.sourceFamily.presets.agent_heartbeat' },
];

export function resolveSourceFamily(source?: Partial<ScenarioSource> | null): ScenarioSourceFamily {
  if (!source) return 'eventlog';
  if (source.kind === 'scheduled-staleness') return 'app-service';
  if (source.kind === 'scheduled-query' || source.kind === 'meta-correlation') return 'app-service';
  const observationKind = String(source.observationKind || 'event').toLowerCase();
  if (observationKind === 'metric') return 'metric';
  if (observationKind === 'signal') return 'app-service';
  return 'eventlog';
}

export function resolveAppServiceMode(source?: Partial<ScenarioSource> | null): AppServiceMode {
  return source?.kind === 'scheduled-staleness' ? 'staleness' : 'live';
}

export function applySourceFamily(
  source: ScenarioSource,
  family: ScenarioSourceFamily,
  appMode: AppServiceMode = 'live',
): ScenarioSource {
  const next: ScenarioSource = {
    ...source,
    dependsOnScenarioIds: source.dependsOnScenarioIds ?? [],
    maxChainDepth: source.maxChainDepth || 5,
    matchKey: source.matchKey || '',
  };

  if (family === 'eventlog') {
    next.kind = 'observation';
    next.observationKind = 'event';
    if (!next.matchKey || next.matchKey === 'event_key' || next.matchKey === 'cpu_usage') {
      next.matchKey = 'rdp.logon';
    }
    return next;
  }

  if (family === 'metric') {
    next.kind = 'observation';
    next.observationKind = 'metric';
    if (!next.matchKey || next.matchKey === 'event_key' || next.matchKey.includes('.')) {
      next.matchKey = 'cpu_usage';
    }
    return next;
  }

  // app-service
  if (appMode === 'staleness') {
    next.kind = 'scheduled-staleness';
    next.observationKind = 'metric';
    if (!next.matchKey) next.matchKey = 'agent_heartbeat';
    return next;
  }

  next.kind = 'observation';
  next.observationKind = 'signal';
  if (!next.matchKey || next.matchKey === 'event_key') next.matchKey = 'service_health';
  return next;
}

export function sourceFamilySubtitle(
  source?: Partial<ScenarioSource> | null,
  translate?: (key: string) => string,
): string {
  const family = resolveSourceFamily(source);
  const key = source?.matchKey?.trim();
  const familyLabel = translate
    ? translate(`alarmCenter.scenarioStudio.sourceFamily.${family}.title`)
    : family;
  return key ? `${familyLabel} · ${key}` : familyLabel;
}

export function presetsForFamily(family: ScenarioSourceFamily): ScenarioSourcePreset[] {
  if (family === 'metric') return METRIC_MATCH_PRESETS;
  if (family === 'app-service') return APP_SERVICE_MATCH_PRESETS;
  return EVENTLOG_MATCH_PRESETS;
}
