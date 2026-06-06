export type AlarmStatus = 'Active' | 'Acknowledged' | 'Resolved' | 'Suppressed';

export interface AlarmSummary {
  id: string;
  ruleId: string;
  dedupKey: string;
  domainId: string;
  domainName: string;
  severity: number;
  status: AlarmStatus;
  firstSeenAt: string;
  lastSeenAt: string;
  count: number;
  correlationId: string;
  context: Record<string, unknown>;
}

export interface AlarmListResponse {
  items: AlarmSummary[];
  total: number;
  skip: number;
  limit: number;
}

export interface AlarmListQuery {
  status?: AlarmStatus;
  minSeverity?: number;
  openOnly?: boolean;
  ruleId?: string;
  search?: string;
  from?: string;
  to?: string;
  skip?: number;
  limit?: number;
}

export interface AlarmScenarioRollup {
  matchKey: string;
  openCount: number;
  totalInRange: number;
  maxSeverity: number | null;
  lastSeenAt: string | null;
}

export interface AlarmDashboardSnapshot {
  from: string;
  to: string;
  openTotal: number;
  openAlarms: AlarmSummary[];
  scenarioRollup: AlarmScenarioRollup[];
}

export interface AlarmDashboardSnapshotQuery {
  rangeHours?: number;
  minSeverity?: number;
  openLimit?: number;
}

export type AlarmRuleType = 'threshold' | 'correlation' | 'scheduled' | 'sequence';

export interface AlarmSequenceStep {
  matchKey: string;
  minCount?: number;
  withinMinutes?: number;
  withinMinutesAfterFirst?: number;
}

export interface AlarmRuleMetadata {
  packageId?: string;
  packageVersion?: string;
  scenarioId?: string;
  description?: string;
  threatTacticId?: string;
  threatTacticName?: string;
  threatTechniqueId?: string;
  threatTechniqueName?: string;
  complianceTags?: string[];
}

export interface AlarmRule {
  id: string;
  domainId: string;
  domainName: string;
  name: string;
  enabled: boolean;
  type: AlarmRuleType | string;
  severity: number;
  matchKey: string;
  operator: string;
  threshold: number;
  dedupKeyTemplate?: string;
  cooldownMinutes: number;
  groupByFields: string[];
  windowMinutes: number;
  stalenessMinutes: number;
  sequenceSteps?: AlarmSequenceStep[];
  metadata?: AlarmRuleMetadata;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateAlarmRuleRequest {
  name: string;
  type?: AlarmRuleType | string;
  severity?: number;
  matchKey: string;
  operator?: string;
  threshold?: number;
  cooldownMinutes?: number;
  groupByFields?: string[];
  windowMinutes?: number;
  stalenessMinutes?: number;
  dedupKeyTemplate?: string;
  sequenceSteps?: AlarmSequenceStep[];
}

export interface UpdateAlarmRuleRequest {
  name?: string;
  enabled?: boolean;
  severity?: number;
  operator?: string;
  threshold?: number;
  cooldownMinutes?: number;
  groupByFields?: string[];
  windowMinutes?: number;
  stalenessMinutes?: number;
  dedupKeyTemplate?: string;
}

export interface AlarmRuleSavePayload {
  isEdit: boolean;
  id?: string;
  body: CreateAlarmRuleRequest | UpdateAlarmRuleRequest;
}
