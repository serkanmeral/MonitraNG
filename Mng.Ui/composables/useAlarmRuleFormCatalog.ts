import type { AlarmRuleType } from '@/types/apps/alarm';

export interface AlarmRuleMatchKeyOption {
  value: string;
  scenarioId?: string;
  descriptionKey: string;
}

export interface AlarmRuleGroupByOption {
  value: string;
  descriptionKey: string;
}

export interface AlarmRuleTypeCardDef {
  type: AlarmRuleType;
  icon: string;
  titleKey: string;
  subtitleKey: string;
}

export const ALARM_RULE_TYPE_CARDS: AlarmRuleTypeCardDef[] = [
  {
    type: 'threshold',
    icon: 'mdi-gauge',
    titleKey: 'alarmCenter.rules.typeThreshold',
    subtitleKey: 'alarmCenter.rules.typeThresholdDesc',
  },
  {
    type: 'correlation',
    icon: 'mdi-vector-link',
    titleKey: 'alarmCenter.rules.typeCorrelation',
    subtitleKey: 'alarmCenter.rules.typeCorrelationDesc',
  },
  {
    type: 'scheduled',
    icon: 'mdi-clock-alert-outline',
    titleKey: 'alarmCenter.rules.typeScheduled',
    subtitleKey: 'alarmCenter.rules.typeScheduledDesc',
  },
];

/** SIEM / observation keys (SEC_EVENT_OBSERVATION_MAP + metric examples). */
export const ALARM_RULE_MATCH_KEY_OPTIONS: AlarmRuleMatchKeyOption[] = [
  { value: 'login_failed', scenarioId: 'U1', descriptionKey: 'alarmCenter.rules.matchKeys.login_failed' },
  { value: 'login_success', scenarioId: 'U2', descriptionKey: 'alarmCenter.rules.matchKeys.login_success' },
  {
    value: 'login_success_after_failures',
    scenarioId: 'U2',
    descriptionKey: 'alarmCenter.rules.matchKeys.login_success_after_failures',
  },
  {
    value: 'privileged_login_outside_window',
    scenarioId: 'U3',
    descriptionKey: 'alarmCenter.rules.matchKeys.privileged_login_outside_window',
  },
  { value: 'denied_flow', scenarioId: 'U4', descriptionKey: 'alarmCenter.rules.matchKeys.denied_flow' },
  { value: 'allowed_flow', scenarioId: 'U5', descriptionKey: 'alarmCenter.rules.matchKeys.allowed_flow' },
  { value: 'rule_change', scenarioId: 'U6', descriptionKey: 'alarmCenter.rules.matchKeys.rule_change' },
  { value: 'new_flow', scenarioId: 'U7', descriptionKey: 'alarmCenter.rules.matchKeys.new_flow' },
  { value: 'group_member_added', scenarioId: 'U8', descriptionKey: 'alarmCenter.rules.matchKeys.group_member_added' },
  { value: 'account_created', scenarioId: 'U9', descriptionKey: 'alarmCenter.rules.matchKeys.account_created' },
  {
    value: 'directory_object_modified',
    scenarioId: 'U10',
    descriptionKey: 'alarmCenter.rules.matchKeys.directory_object_modified',
  },
  { value: 'cpu_usage', descriptionKey: 'alarmCenter.rules.matchKeys.cpu_usage' },
  { value: 'disk_free_percent', descriptionKey: 'alarmCenter.rules.matchKeys.disk_free_percent' },
];

export const ALARM_RULE_GROUP_BY_OPTIONS: AlarmRuleGroupByOption[] = [
  { value: 'userId', descriptionKey: 'alarmCenter.rules.groupByFields.userId' },
  { value: 'srcIp', descriptionKey: 'alarmCenter.rules.groupByFields.srcIp' },
  { value: 'dstIp', descriptionKey: 'alarmCenter.rules.groupByFields.dstIp' },
  { value: 'dstPort', descriptionKey: 'alarmCenter.rules.groupByFields.dstPort' },
  { value: 'sourceHost', descriptionKey: 'alarmCenter.rules.groupByFields.sourceHost' },
  { value: 'sourceType', descriptionKey: 'alarmCenter.rules.groupByFields.sourceType' },
];

export function defaultDedupTemplate(type: AlarmRuleType): string {
  if (type === 'correlation') return '{ruleId}:{groupKey}';
  return '{ruleId}:{key}';
}

export function severityBand(severity: number): 'low' | 'medium' | 'high' | 'critical' {
  if (severity >= 9) return 'critical';
  if (severity >= 7) return 'high';
  if (severity >= 4) return 'medium';
  return 'low';
}

export function operatorSymbol(operator: string): string {
  switch (operator) {
    case 'gt':
      return '>';
    case 'gte':
      return '≥';
    case 'lt':
      return '<';
    case 'lte':
      return '≤';
    case 'eq':
      return '=';
    default:
      return operator;
  }
}

export function typeLabelKey(type: string): string {
  switch (type) {
    case 'threshold':
      return 'alarmCenter.rules.typeThresholdShort';
    case 'correlation':
      return 'alarmCenter.rules.typeCorrelationShort';
    case 'scheduled':
      return 'alarmCenter.rules.typeScheduledShort';
    case 'sequence':
      return 'alarmCenter.rules.typeSequenceShort';
    default:
      return 'alarmCenter.rules.typeUnknown';
  }
}
