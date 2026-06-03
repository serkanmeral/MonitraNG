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
  skip?: number;
  limit?: number;
}

export type AlarmRuleType = 'threshold' | 'correlation' | 'scheduled';

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
